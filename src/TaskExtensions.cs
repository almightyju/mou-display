using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LcdScreenApp;
internal static class TaskEx
{
    public static async Task DelayNoThrow(int miliseconds, CancellationToken cancellationToken)
    {
        try { await Task.Delay(miliseconds, cancellationToken); }
        catch(TaskCanceledException) { }
    }
}
