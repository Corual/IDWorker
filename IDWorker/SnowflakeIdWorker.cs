using System;
using System.Collections.Generic;
using System.Text;

namespace IDWorker
{
    /// <summary>
    /// 基于Snowflake算法的分布式ID生成器
    /// 雪花算法使用长度64位的long类型分不同部分生成
    /// 最左1位是符号位不用，剩下63位，剩余部分从左到右，分别：41位时间戳部分+5位数据中心部分+5位机器号部分+12位序号部分
    /// 41位时间戳从自定义的开始时间开始，可以使用69年之久
    /// 5位数据中心+5位机器号，可以达到32*32=1024个分布式节点
    /// 12位序号可以保证同一毫秒最大生成4096个id（0~4095）
    /// 算法核心思想：分别生成各部分数据，通过左移操作把各部分数据定位好，最后通过位或操作把各部分二进制合并起来，返回合并后二进制对应的long数据
    /// </summary>
    public class SnowflakeIdWorker : IIDWorker
    {


        #region 各部分数据左移长度
        /// <summary>
        /// 时间戳部分长度
        /// </summary>
        private const int TIME_lEN = 41;

        /// <summary>
        /// 数据中心部分长度
        /// </summary>
        private const int DATA_CENTER_LEN = 5;

        /// <summary>
        /// 机器号部分长度
        /// </summary>
        private const int MACHINE_LEN = 5;

        /// <summary>
        /// 序号部分长度
        /// </summary>
        private const int SEQUENCE_LEN = 12;

        /// <summary>
        /// 全部加起来的长度（省略符号位不计算在内）
        /// </summary>
        private const int FULL_LEN = 63;
        #endregion

        #region 各部分需要左移的二进制位数
        /// <summary>
        /// 时间戳部分左移的位数
        /// </summary>
        private const int TIME_lEFT_BIT = FULL_LEN - TIME_lEN;

        /// <summary>
        /// 数据中心部分左移的位数
        /// </summary>
        private const int DATA_CENTER_LEFT_BIT = TIME_lEFT_BIT - DATA_CENTER_LEN;

        /// <summary>
        /// 机器号部分左移的位数
        /// </summary>
        private const int MACHINE_LEFT_BIT = DATA_CENTER_LEFT_BIT - MACHINE_LEN;
        #endregion

        #region 各部分数据的最大值
        /// <summary>
        /// 最大序号
        /// </summary>
        private const int MAX_SEQUENCE = ~(-1 << SEQUENCE_LEN);

        /// <summary>
        /// 最大数据中心个数
        /// </summary>
        private const int MAX_DATA_CENTER_COUNT = ~(-1 << DATA_CENTER_LEN);

        /// <summary>
        /// 每个数据中心最大的机器节点个数
        /// </summary>
        private const int MAX_MACHINE_NODE = ~(-1 << MACHINE_LEN);
        #endregion


        #region 一些字段初始化
        /// <summary>
        /// ID开始生成的基准时间戳
        /// </summary>
        private long _startTimestamp = long.MinValue;

        /// <summary>
        /// 上一次生成ID的时间戳
        /// </summary>
        private long _lastIdTimestamp = long.MinValue;

        /// <summary>
        /// 数据中心ID
        /// </summary>
        private long _datacenterId = -1L;

        /// <summary>
        /// 机器号
        /// </summary>
        private long _machineId = -1L;

        /// <summary>
        /// 上一次同一毫秒内生成的序号
        /// </summary>
        private long _lastSequence = 0L;

        /// <summary>
        /// 格林威治Tick
        /// 1Ticks = 0.0001毫秒
        /// </summary>
        private const long GREENWICH_TICKS = 621355968000000000L;

        /// <summary>
        /// 同步锁
        /// </summary>
        private static object _syncLock = new object();
        #endregion

        public SnowflakeIdWorker(DateTime startTime, long datacenterId = -1L, long machineId = -1L) : this(-1L, datacenterId, machineId)
        {
            _startTimestamp = GetTimestamp(startTime);
        }

        public SnowflakeIdWorker(long startTimestamp, long datacenterId = -1L, long machineId = -1L)
        {
            _startTimestamp = startTimestamp;
            _datacenterId = GetDatacenterId(datacenterId);
            _machineId = GetMachineId(machineId);
        }


        /// <summary>
        /// 获取数据中心ID
        /// </summary>
        /// <param name="datacenterId"></param>
        /// <returns>如果调用方没有传入，就随机分配一个</returns>
        private long GetDatacenterId(long datacenterId)
        {
            return datacenterId > -1 ? datacenterId : (new Random().Next(0, MAX_DATA_CENTER_COUNT + 1));
        }

        /// <summary>
        /// 获取机器号
        /// </summary>
        /// <param name="machineId"></param>
        /// <returns>如果调用方没有传入，就随机分配一个</returns>
        private long GetMachineId(long machineId)
        {
            return machineId > -1 ? machineId : (new Random().Next(0, MAX_MACHINE_NODE + 1));
        }


        /// <summary>
        /// 获取当前系统时间戳
        /// </summary>
        /// <returns></returns>
        private long GetSystemTimestamp()
        {
            //return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            //使用这种，可以省去构造new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)的时间，直接计算结果
            //return (DateTime.UtcNow.Ticks - GREENWICH_TICKS) / 10000;
            return (DateTime.UtcNow.Ticks - GREENWICH_TICKS) / 10000;
        }

        private long GetTimestamp(DateTime date)
        {
            return (date.ToUniversalTime().Ticks - GREENWICH_TICKS) / 10000;
        }


        /// <summary>
        /// 自旋等待下一毫秒
        /// </summary>
        /// <param name="timestamp">当前时间戳</param>
        /// <returns></returns>
        private long SpinWaitNextMillisecond(long timestamp)
        {
            long newTimestamp = GetSystemTimestamp();
            while (timestamp >= newTimestamp)
            {
                newTimestamp = GetSystemTimestamp();
            }

            return newTimestamp;
        }

        /// <summary>
        /// 生成一个分布式ID
        /// </summary>
        /// <returns>
        /// 返回值：
        ///     返回long类型的Id
        /// 异常:
        ///   TimeRegressionException:
        ///     当检测到系统时间回拨的时候会抛出此异常
        /// </returns>
        public long NextId()
        {
            lock (_syncLock)
            {
                //获取当前时间戳
                long currentTimestamp = GetSystemTimestamp();

                //时间特性是越后的时间越大，所以如果当前时间戳比上一次生成ID的时间戳还少，就证明系统时钟在回拨
                if (currentTimestamp < _lastIdTimestamp)
                {
                    throw new TimeRegressionException(string.Format("系统时间异常！ 发现系统时钟出现回拨现象，终止生成雪花ID！"));
                }


                if (currentTimestamp == _lastIdTimestamp)
                {
                    //同一毫秒内触发生成id请求，需要递增序号
                    _lastSequence = MAX_SEQUENCE & (_lastSequence + 1); //最大值跟当前值进行二进制&运算，只要_lastSequence + 1还没有到最大值，结果都会返回_lastSequence + 1，如果超了最大值，那么运算结果就是(_lastSequence + 1 - MAX_SEQUENCE)

                    if (0 == _lastSequence)
                    {
                        //运算后等于0，证明当前毫秒内的序号已经分派完，需要强制等待下一毫秒（因为默认为0然后 MAX_SEQUENCE & (_lastSequence + 1)运算导致序号从1开始，因此当变回0的时候表示id派完）
                        //由于通过WaitNextMillisecond得到新的毫秒值，所以这里的需要不用重新递增，遵循不同毫秒内从0开始的规则
                        currentTimestamp = SpinWaitNextMillisecond(_lastIdTimestamp);
                    }

                }
                else
                {
                    //不在同一毫秒发生的id请求，需要将序号重置为0，避免出现同毫秒请求时浪费一些序号
                    _lastSequence = 0;
                }

                //更新上一次时间戳变量，保存当前时间戳到该变量
                _lastIdTimestamp = currentTimestamp;


                return ((currentTimestamp - _startTimestamp) << TIME_lEFT_BIT) | (_datacenterId << DATA_CENTER_LEFT_BIT) | (_machineId << MACHINE_LEFT_BIT) | _lastSequence;
            }
        }
    }
}
