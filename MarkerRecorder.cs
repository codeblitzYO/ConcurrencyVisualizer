using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace ETW
{
    public struct Marker : IRecordData
    {
        public enum Event
        {
            EnterSpan = 1,
            LeaveSpan,
            Flag,
            Message,
            Manifest = 0xfffe
        }

        public Event e;
        public int id;
        public string name;
        public int thread;
        public DateTime timestamp;

        public DateTime Timestamp
        {
            get { return timestamp; }
            set { timestamp = value; }
        }
    }

    class MarkerRecorder : EventRecorder
    {
        private Record<Marker> markerRecord = new Record<Marker>();

        public MarkerRecorder()
        {
        }
        public MarkerRecorder(Guid guid)
        {
            Start(guid);
        }

        public override void Stop()
        {
            base.Stop();
        }

        protected override void InitializeProviders(Guid guid)
        {
            Session.Source.AllEvents += OnEvent;
            Session.EnableProvider(guid, TraceEventLevel.Informational);
        }

        protected void OnEvent(TraceEvent data)
        {
            if (data.Version != 1) return;
            if ((int)data.ID == 0xfffe) return;

            var e = (Marker.Event)data.ID;
            if (e == Marker.Event.Manifest) return;

            var bytes = data.EventData();
            var level = (int)bytes[1];
            var category = (int)bytes[2];
            var span = (int)0;
            var textFieldOffset = 0;

            switch (e)
            {
                case Marker.Event.EnterSpan:
                case Marker.Event.LeaveSpan:
                    span = BitConverter.ToInt32(bytes, 3);
                    textFieldOffset = 4 + 3;
                    break;
                default:
                    textFieldOffset = 3 + 3;
                    break;
            }
            var series = Encoding.Unicode.GetString(bytes, textFieldOffset, RawData.getUnicodeBytes(bytes, textFieldOffset));
            textFieldOffset += (series.Length + 1) * 2;
            var name = Encoding.Unicode.GetString(bytes, textFieldOffset, RawData.getUnicodeBytes(bytes, textFieldOffset));
            if (string.IsNullOrEmpty(name))
            {
                textFieldOffset += (name.Length + 1) * 2;
                name = Encoding.UTF8.GetString(bytes, textFieldOffset, RawData.getMulticharBytes(bytes, textFieldOffset));
                if (name.Length >= 8)
                {
                    name = name.Remove(0, 8); // コードページ削除
                }
            }

            markerRecord.Append(new Marker()
            {
                e = e,
                id = span,
                name = name,
                thread = data.ThreadID,
                timestamp = data.TimeStamp
            });
        }

        public List<Marker> GetMarkerSpan(DateTime startTime, DateTime lastTime)
        {
            return markerRecord.GetSpan(startTime, lastTime);
        }
    }
}
