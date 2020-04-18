using System;
using System.Threading.Tasks;

namespace Loxone.Client.Transport
{
    public static class TaskUtilities
    {
        #pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void
        internal static async void FireAndForgetSafeAsync(this Task task, IErrorHandler handler = null)
        #pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                handler?.HandleError(ex);
            }
        }
    }
}
