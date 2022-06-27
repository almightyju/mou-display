using System.IO.Ports;
using System.Management;
using System.Runtime.Versioning;

namespace LcdScreenApp;

public class Worker : BackgroundService
{
    readonly ILogger<Worker> _logger;
    readonly CancellationTokenSource _workerTokenSource;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _workerTokenSource = new();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        HardwareMonitor hwMon = new(_workerTokenSource.Token);

        while (!_workerTokenSource.IsCancellationRequested)
        {
            SerialPort? port = await GetComPort();
            if(port == null)
                await Task.Delay(1000, _workerTokenSource.Token);
            else 
            { 
                _logger.LogInformation("Port opened {port}", port.PortName);
                Display disp = new(port, hwMon);
                await disp.WaitTillClosed(_workerTokenSource.Token);
                if(!_workerTokenSource.IsCancellationRequested)
                    _logger.LogInformation("Port lost {port}", port.PortName);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _workerTokenSource.Cancel();
        await base.StopAsync(cancellationToken);
        await ExecuteTask;
        _logger.LogInformation("Service stopped");
    }

    static async ValueTask<SerialPort> GetComPort()
    {
        //send [0xFE 0x37] and expect 0x53 which means got device correct
        //then send [0xFE 0x42 0x0] which means turn screen on, 0x0 = no timeout for off
        //baud rate is 19200
        string? portName = null;

        while (portName == null)
        {
            if (OperatingSystem.IsWindows())
                portName = await GetComPortWindows();
            else
                throw new NotImplementedException();

            if (portName == null)
                await Task.Delay(1000);
        }

        SerialPort port = new(portName)
        {
            BaudRate = 19200
        };
        port.Open();

        return port;
    }

    [SupportedOSPlatform("windows")]
    static ValueTask<string?> GetComPortWindows()
    {
        string[] portNames = SerialPort.GetPortNames();

        using ManagementObjectSearcher searcher = new("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'");
        string[] captions = searcher.Get()
            .Cast<ManagementBaseObject>()
            .Select(i => i["Caption"].ToString() ?? string.Empty)
            .Where(i => i != string.Empty)
            .ToArray();

        string? portName = portNames.Select(i => (port: i, name: captions.FirstOrDefault(c => c.Contains($"({i})")) ?? string.Empty))
            .Where(i => i.name != string.Empty && i.name.StartsWith("Matrix Orbital"))
            .Select(i => i.port)
            .FirstOrDefault();

        return new(portName);
    }
}
