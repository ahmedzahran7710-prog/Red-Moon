using Canyon.Login.Managers;
using Canyon.Shared;
using Quartz;

namespace Canyon.Login.Threading
{
    [DisallowConcurrentExecution]
    public sealed class BasicThread : IJob
    {
        public BasicThread()
        {
        }

        public Task Execute(IJobExecutionContext context)
        {
            DeveloperRightsGuard.EnforceOrTerminate(typeof(BasicThread).Assembly);
            Console.Title = $"[{DeveloperRightsGuard.BrandName}] Login Server (by {DeveloperRightsGuard.DeveloperId}) - Servers[{RealmManager.Count}] - {DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}";



            return Task.CompletedTask;
        }

    }
}
