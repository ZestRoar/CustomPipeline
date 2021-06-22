using System;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace PipePerformanceTest
{
    public class PerformanceHelper
    {
#pragma warning disable CA1416 // 플랫폼 호환성 유효성 검사
        private Stopwatch stopwatch;
        private PerformanceCounter cpuCounter;
        private PerformanceCounter processCounter;
        private PerformanceCounter ramCounter;
        private PerformanceCounter ramValCounter;
        private PerformanceCounter diskIOCounter;

        private RateUsageInfo cpuLog;
        private RateUsageInfo processLog;
        private RateUsageInfo ramLog;
        private RateUsageInfo ramValLog;
        private RateUsageInfo diskIOLog;

        private string processName;

        public PerformanceHelper()
        {
            processName = Process.GetCurrentProcess().ProcessName;

            cpuLog = new RateUsageInfo("%");
            processLog = new RateUsageInfo("%");
            ramLog = new RateUsageInfo("%");
            ramValLog = new RateUsageInfo("KB");
            diskIOLog = new RateUsageInfo("%");
            this.stopwatch = new Stopwatch();
            this.cpuCounter = new PerformanceCounter("Processor", @"% Processor Time", @"_Total", true);
            this.processCounter = new PerformanceCounter("Process", @"% Processor Time", processName);
            this.ramCounter = new PerformanceCounter("Memory", "Available MBytes", true);
            this.ramValCounter = new PerformanceCounter("Process", "Working Set", processName, true);

            string drive = AppDomain.CurrentDomain.BaseDirectory.Substring(0, 2).ToUpper();
            string[] instanceNameArray = new PerformanceCounterCategory("PhysicalDisk").GetInstanceNames();
            string instanceName = instanceNameArray.FirstOrDefault(s => s.IndexOf(drive) > -1);
            this.diskIOCounter = new PerformanceCounter("PhysicalDisk", "% Idle Time", instanceName, true);
        }

        public void StartTimer()
        {
            this.stopwatch.Start();
        }

        public void StopTimer()
        {
            this.stopwatch.Stop();
        }

        public string ElapsedToStringFormat()
        {
            TimeSpan ts = stopwatch.Elapsed;
            return $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
        }

        public float GetCPURate()
        {
            return this.cpuCounter.NextValue();
        }
        public float GetProcessRate()
        {
            return this.processCounter.NextValue();
        }

        public float GetMemoryRate()
        {
            using (ManagementClass manage = new ManagementClass("Win32_OperatingSystem"))
            {
                using (ManagementObject obj = manage.GetInstances().Cast<ManagementObject>().FirstOrDefault())
                {
                    float physicalMemorySize = float.Parse(obj["TotalVisibleMemorySize"].ToString());
                    return (physicalMemorySize - this.ramCounter.NextValue()) / physicalMemorySize * 100;
                }
            }
        }
        public float GetMemoryValue()
        {
            return ramValCounter.NextValue()/1024;
        }

        public float GetDiskIORate()
        {
            return 100 - this.diskIOCounter.NextValue();
        }

        public void CheckAllUsage()
        {
            cpuLog.CheckInfo(GetCPURate());
            //processLog.CheckInfo(GetProcessRate());
            //ramLog.CheckInfo(GetMemoryRate());
            ramValLog.CheckInfo(GetMemoryValue());
            diskIOLog.CheckInfo(GetDiskIORate());
        }

        public string GetCPUInfo()
        {
            return this.cpuLog.ToString();
        }
        public string GetProcessInfo()
        {
            return this.processLog.ToString();
        }
        public string GetMemoryRateInfo()
        {
            return this.ramLog.ToString();
        }
        public string GetMemoryInfo()
        {
            return this.ramValLog.ToString();
        }
        public string GetDiskIOInfo()
        {
            return this.diskIOLog.ToString();
        }
#pragma warning restore CA1416 // 플랫폼 호환성 유효성 검사

    }

    public struct RateUsageInfo
    {
        public RateUsageInfo(string unit)
        {
            count = 0;
            min = -1;
            max = 0;
            sum = 0;
            this.unit = unit;
        }

        public int count;
        public float min;
        public float max;
        public double sum;
        public string unit;

        public void CheckInfo(float rate)
        {
            ++count;
            if (rate < 0)
            {
                rate = 0;
            }
            if (min < 0)
            {
                min = rate;
            }
            min = MathF.Min(min, rate);
            max = MathF.Max(max, rate);
            sum += rate;
        }
        public float GetAverage()
        {
            if (count == 0)
            {
                return 0;
            }
            return (float)(sum / count);
        }

        public string ToString()
        {
            return $"average = {GetAverage().ToString()} {unit} ( min : {min.ToString()}, max {max.ToString()} )";
        }
    }
}
