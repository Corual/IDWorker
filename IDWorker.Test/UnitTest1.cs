using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IDWorker.Test
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ConvertToBinaryString()
        {

            Console.WriteLine($"Min:{Convert.ToString(long.MinValue, 2)}");
            Console.WriteLine($"Max:{Convert.ToString(long.MaxValue, 2)}");
            Assert.Pass();

        }

        [Test]
        public void ConvertToNumber()
        {
            Console.WriteLine(Convert.ToInt32("011", 2));
            Console.WriteLine(Convert.ToInt64(Convert.ToString(long.MaxValue, 2), 2));
            Console.WriteLine(~(-1L << 12));
            Assert.AreEqual(long.MaxValue, ~(-1L << 63));
        }

        [Test]
        public void GetTimestamp()
        {
            DateTime time = DateTime.UtcNow;
            DateTime time2 = DateTime.Now;
            var timestampToUTC = (time2.ToUniversalTime().Ticks - 621355968000000000) / 10000;
            var timestamp = (time.Ticks - 621355968000000000) / 10000;
            var timestampNewDateObject = (long)(time - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            Console.WriteLine(timestampToUTC);
            Console.WriteLine(timestamp);
            Console.WriteLine(timestampNewDateObject);
            Assert.AreEqual(timestampToUTC, timestamp);
            Assert.AreEqual(timestampToUTC, timestampNewDateObject);
        }

        [Test]
        public void IDWorkTest()
        {
            long startTimestmap = (long)(DateTime.UtcNow.AddDays(-20) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            IIDWorker iDWorker = new SnowflakeIdWorker(startTimestmap, 10, 1);
            int count = 300 * 10000;
            HashSet<long> idSet = new HashSet<long>();
            object testLock = new object();
            AutoResetEvent autoResetEvent = new AutoResetEvent(false);
            int invokeCount = 0;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    Task.Run(() =>
                    {
                        var id = iDWorker.NextId();
                        lock (testLock)
                        {
                            if (!idSet.Add(id))
                            {
                                autoResetEvent.Set();
                                return;
                            }
                        }

                        Console.WriteLine(id);
                        if (count == Interlocked.Increment(ref invokeCount))
                        {
                            autoResetEvent.Set();
                            Console.WriteLine("go!");
                        }
                    });

                }

                autoResetEvent.WaitOne();
            }
            finally
            {
                autoResetEvent.Dispose();
            }
            Assert.AreEqual(count, idSet.Count);
            Assert.AreEqual(count, invokeCount);
        }


    }
}