using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Parsers;
using System.Collections;

namespace ETW
{
    public interface IRecordData
    {
        DateTime Timestamp { get; set; }
    }

    public class Record<T> where T : IRecordData
    {
        private Queue<T> dataArray;
        private object dataLock;

        public TimeSpan Duration { set; get; }
        

        public Record()
        {
            Duration = TimeSpan.FromSeconds(10);
            dataArray = new Queue<T>();
            dataLock = new object();
        }

        public void Append(T value)
        {
            lock (dataLock)
            {
                dataArray.Enqueue(value);

                var leastTime = value.Timestamp - Duration;
                while (dataArray.Count > 0)
                {
                    if (dataArray.Peek().Timestamp > leastTime) break;
                    dataArray.Dequeue();
                }
            }
        }

        private IEnumerable<T> SpanEnum(DateTime startTime, DateTime lastTime)
        {
            foreach (var e in dataArray)
            {
                if (e.Timestamp >= startTime && e.Timestamp < lastTime)
                {
                    yield return e;
                }
                else if (e.Timestamp >= lastTime) break;
            }
        }

        public List<T> GetSpan(DateTime startTime, DateTime lastTime)
        {
            lock (dataLock)
            {
                return new List<T>(SpanEnum(startTime, lastTime));
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

        public abstract string SessionName { get; }

        public virtual void Start()
        {
            etwSesion = new TraceEventSession(SessionName);
            etwSesion.StopOnDispose = true;

            InitializeProviders();

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

        protected abstract void InitializeProviders();
    }
}
