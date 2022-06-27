using System;
using System.ComponentModel;
using System.IO.Ports;

namespace LcdScreenApp;
internal class Display
{
    class LineText
    {
        public delegate void TextChangedHandler(string oldValue, string newValue);
        public event TextChangedHandler? TextChanged;

        string _text = string.Empty;
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                    TextChanged?.Invoke(_text, value);
                _text = value;
            }
        }
    }


    LineText Line1 { get; init; }
    LineText Line2 { get; init; }

    readonly SerialPort _port;
    readonly HardwareMonitor _hwMon;

    public Display(SerialPort port, HardwareMonitor hwMon)
    {
        _port = port;
        _hwMon = hwMon;

        Line1 = new();
        Line2 = new();
        Line1.TextChanged += (o, n) => LineChanged(1, o, n);
        Line2.TextChanged += (o, n) => LineChanged(2, o, n);
    }

    internal async Task WaitTillClosed(CancellationToken cancelToken)
    {
        ScreenOn();
        ClearScreen();

        Line1.Text = GetDateString();
        Line2.Text = GetTempString();

        _hwMon.PropertyChanged += HwMon_PropertyChanged;

        while (!cancelToken.IsCancellationRequested && _port.IsOpen)
        {
            await TaskEx.DelayNoThrow(200, cancelToken);
            Line1.Text = GetDateString();
        }

        if (_port.IsOpen)
        {
            ScreenOff();
            _port.Close();
        }
        _port.Dispose();

        _hwMon.PropertyChanged -= HwMon_PropertyChanged;
    }


    static readonly char[] _prefixSuffix = new[] {'|', '/', '-', '\\' };

    static string GetDateString() 
    {
        DateTime now = DateTime.Now;
        int charIdx = now.Second % 4;
        char prefixSuffix = _prefixSuffix[charIdx];

        return $"{prefixSuffix} {now:dd MMM  hh:mm tt} {prefixSuffix}";
    } 

    string GetTempString()
    {
        int cpuTemp = _hwMon.CpuTemps.FirstOrDefault();
        int gpuTemp = _hwMon.GpuTemps.FirstOrDefault();
        if (cpuTemp == 0 && gpuTemp == 0)
            return "                    ";

        string cpu = cpuTemp.ToString().PadLeft(2, '0');
        string gpu = gpuTemp.ToString().PadLeft(2, '0');
        return $" CPU: {cpu}c  GPU: {gpu}c ";
    }

    void HwMon_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Line2.Text = GetTempString();
    }

    void LineChanged(byte lineNumber, string oldVal, string newVal)
    {
        for(byte i = 0; i <= 20; i++)
        {
            char oldC = oldVal.Length > i ? oldVal[i] : ' ';
            char newC = newVal.Length > i ? newVal[i] : ' ';

            if (oldC == newC)
                continue;

            byte[] setPosCmd = new byte[] { 0xFE, 0x47, (byte)(i + 1), lineNumber };
            _port.Write(setPosCmd, 0, setPosCmd.Length);
            _port.Write(newC.ToString());
        }
    }

    void ClearScreen() =>
        _port.Write(new byte[] { 0xFE, 0x58 }, 0, 2);

    void ScreenOff() =>
        _port.Write(new byte[] { 0xFE, 0x46 }, 0, 2);

    void ScreenOn() =>
        _port.Write(new byte[] { 0xFE, 0x42, 0x0 }, 0, 3);
}
