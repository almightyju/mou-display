using System.ComponentModel;
using System.Management;
using System.Runtime.Versioning;

namespace LcdScreenApp;

internal class HardwareMonitor : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;


    public IEnumerable<int> GpuTemps => _gpuTemps;
    public IEnumerable<int> CpuTemps => _cpuTemps;
    
    List<int> _gpuTemps = new();
    List<int> _cpuTemps = new();


    public HardwareMonitor(CancellationToken cancelToken)
    {
        if (OperatingSystem.IsWindows())
#pragma warning disable CA1416 // Validate platform compatibility
            Task.Factory.StartNew(() => GetHwInfoWindows(cancelToken), cancelToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
#pragma warning restore CA1416 // Validate platform compatibility
        else
            throw new PlatformNotSupportedException();
    }

    [SupportedOSPlatform("windows")]
    async Task GetHwInfoWindows(CancellationToken cancelToken)
    {
        ManagementClass sensors = new("\\\\.\\root\\OpenHardwareMonitor", "Sensor", new());
        while (!cancelToken.IsCancellationRequested)
        {
            ManagementObjectCollection instances = sensors.GetInstances();
            List<int> cpuTemps = new();
            List<int> gpuTemps = new();

            foreach(ManagementObject instance in instances)
            {
                string? sensorType = instance.GetPropertyValue("SensorType") as string;
                if (sensorType != "Temperature")
                    continue;
                if (instance.GetPropertyValue("Name") is not string name)
                    continue;

                int value;
                object oValue = instance.GetPropertyValue("Value");
                if (oValue is double dValue)
                    value = (int)Math.Round(dValue);
                else if (oValue is float fValue)
                    value = (int)Math.Round(fValue);
                else
                    continue;

                if(name == "CPU Package")
                    cpuTemps.Add(value);
                else if(name == "GPU Core")
                    gpuTemps.Add(value);
            }

            CompareAndRaiseChanges(cpuTemps, gpuTemps);

            await Task.Delay(1000, cancelToken);
        }
    }

    void CompareAndRaiseChanges(List<int> cpuTemps, List<int> gpuTemps)
    {
        if(!_cpuTemps.SequenceEqual(cpuTemps))
        {
            _cpuTemps = cpuTemps;
            PropertyChanged?.Invoke(this, new(nameof(CpuTemps)));
        }
        if (!_gpuTemps.SequenceEqual(gpuTemps))
        {
            _gpuTemps = gpuTemps;
            PropertyChanged?.Invoke(this, new(nameof(GpuTemps)));
        }
    }
}
