using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IO.Swagger.Tasks
{
    public class TaskControlService
    {
        public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);
    }
}
