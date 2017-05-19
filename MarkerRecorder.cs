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
        private Dictionary<int, Dictionary<Guid, Record<Marker>>> markerThreadsProvidersRecord = new Dictionary<int, Dictionary<Guid, Record<Marker>>>();
        private object markerThreadsProvidersRecordLock = new object();

        public override string SessionName { get { return "MarkerRecorder"; } }
        public static readonly Guid[] ProvidersGuid = {
            new Guid("8d4925ab-505a-483b-a7e0-6f824a07a6f0"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece00"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece01"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece02"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece03"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece04"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece05"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece06"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece07"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece08"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece09"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece0a"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece0b"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece0c"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece0d"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece0e"),
            new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece0f"),
        };

        public MarkerRecorder()
        {
        }

        protected override void InitializeProviders()
        {
            foreach (var i in ProvidersGuid)
            {
                Session.EnableProvider(i);
            }
            Session.Source.AllEvents += OnEvent;
        }

        private Record<Marker> FindMarkerRecord(int threadId, Guid provider, bool create = false)
        {
            Dictionary<Guid, Record<Marker>> threadRecord = null;
            Record<Marker> markerRecord = null;
            lock (markerThreadsProvidersRecordLock)
            {
                markerThreadsProvidersRecord.TryGetValue(threadId, out threadRecord);
                if (threadRecord != null)
                {
                    if (threadRecord.TryGetValue(provider, out markerRecord))
                    {
                        return markerRecord;
                    }
                }

                if (create)
                {
                    if (threadRecord == null)
                    {
                        threadRecord = new Dictionary<Guid, Record<Marker>>();
                        markerThreadsProvidersRecord.Add(threadId, threadRecord);
                    }
                    if (markerRecord == null)
                    {
                        markerRecord = new Record<Marker>();
                        threadRecord.Add(provider, markerRecord);
                    }
                    return markerRecord;
                }
            }
            return null;
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

            var markerRecord = FindMarkerRecord(data.ThreadID, data.ProviderGuid, true);
            if (markerRecord != null)
            {
                markerRecord.Append(new Marker()
                {
                    e = e,
                    id = span,
                    name = name,
                    thread = data.ThreadID,
                    timestamp = data.TimeStamp
                });
            }
        }

        public List<Marker> GetMarkerSpan(int threadId, int providerIndex, DateTime startTime, DateTime lastTime)
        {
            var markerRecord = FindMarkerRecord(threadId, ProvidersGuid[providerIndex]);
            if (markerRecord != null)
            {
                return markerRecord.GetSpan(startTime, lastTime);
            }
            return null;
        }
    }
}
