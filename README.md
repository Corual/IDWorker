# IDWorker
一个分布式ID生成算法 .NET Standard >= 1.1

## .NET Standard
.NET Standard >= 1.1

## 用法
```C#
//获取一个开始时间戳
long startTimestmap = (long)(DateTime.UtcNow.AddDays(-20) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

//使用时间戳构造
IIDWorker iDWorker = new SnowflakeIdWorker(startTimestmap, 10, 1);

//或者直接使用DateTime构造
//IIDWorker iDWorker = new SnowflakeIdWorker(DateTime.AddDays(-20), 10, 1);

//获得Id
long id = iDWorker.NextId();
```
