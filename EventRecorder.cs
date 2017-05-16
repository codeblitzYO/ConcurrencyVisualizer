using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace ETW
{
    public interface IRecordData
    {
        DateTime Timestamp { get; set; }
    }

    public class Record<T> where T : IRecordData
    {
        private List<T> dataArray;
        private object dataLock;

        public TimeSpan Duration { set; get; }
        

        public Record()
        {
            Duration = TimeSpan.FromSeconds(10);
            dataArray = new List<T>();
            dataLock = new object();
        }

        public void Append(T value)
        {
            lock (dataLock)
            {
                dataArray.Add(value);

                var leastTime = value.Timestamp - Duration;
                int count = 0;
                foreach (var i in dataArray)
                {
                    if (i.Timestamp > leastTime) break;
                    ++count;
                }

                dataArray.RemoveRange(0, count);
            }
        }

        public List<T> GetSpan(DateTime startTime, DateTime lastTime)
        {
            lock (dataLock)
            {
                return dataArray.FindAll(e => e.Timestamp >= startTime && e.Timestamp < lastTime);
            }
        }
    }

    public abstract class EventRecorder
    {
        private TraceEventSession etwSesion;
        private Thread runner;

        protected TraceEventSession Session
        {
            get { return etwSesion; }
        }

        public DateTime LastestEventTime
        {
            get;
            protected set;
        }

        public virtual bool IsKernel { get { return false; } }

        public virtual void Start(Guid guid)
        {
            var sessionName = IsKernel ? KernelTraceEventParser.KernelSessionName : "Session" + guid.ToString();

            etwSesion = new TraceEventSession(sessionName);
            etwSesion.StopOnDispose = true;

            InitializeProviders(guid);

            runner = new Thread(() =>
            {
                etwSesion.Source.Process();
            });
            runner.Start();
        }

        public virtual void Stop()
        {
            if (etwSesion != null)
            {
                etwSesion.Stop();
            }
            runner = null;
            etwSesion = null;
        }

        protected abstract void InitializeProviders(Guid guid);
    }
}
