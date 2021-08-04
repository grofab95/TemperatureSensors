using Akka.Actor;
using Akka.Event;
using System;
using System.Collections.Generic;
using TemperatureSensors.Messages;

namespace TemperatureSensors.Actors
{
    public class DeviceGroupActor : UntypedActor
    {
        private Dictionary<string, IActorRef> deviceIdToActor = new Dictionary<string, IActorRef>();
        private Dictionary<IActorRef, string> actorToDeviceId = new Dictionary<IActorRef, string>();

        public DeviceGroupActor(string groupId)
        {
            GroupId = groupId;
        }

        protected override void PreStart() => Log.Info($"Device group {GroupId} started");
        protected override void PostStop() => Log.Info($"Device group {GroupId} stopped");

        protected ILoggingAdapter Log { get; } = Context.GetLogger();
        protected string GroupId { get; }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case RequestAllTemperatures r:
                    Context.ActorOf(DeviceGroupQueryActor.Props(actorToDeviceId, r.RequestId, Sender, TimeSpan.FromSeconds(3)));
                    break;

                case RequestTrackDevice trackMsg when trackMsg.GroupId.Equals(GroupId):
                    if (deviceIdToActor.TryGetValue(trackMsg.DeviceId, out var actorRef))
                    {
                        actorRef.Forward(trackMsg);
                    }
                    else
                    {
                        Log.Info($"Creating device actor for {trackMsg.DeviceId}");
                        var deviceActor = Context.ActorOf(DeviceActor.Props(trackMsg.GroupId, trackMsg.DeviceId), $"device-{trackMsg.DeviceId}");
                        Context.Watch(deviceActor);
                        actorToDeviceId.Add(deviceActor, trackMsg.DeviceId);
                        deviceIdToActor.Add(trackMsg.DeviceId, deviceActor);
                        deviceActor.Forward(trackMsg);
                    }
                    break;
                case RequestTrackDevice trackMsg:
                    Log.Warning($"Ignoring TrackDevice request for {trackMsg.GroupId}. This actor is responsible for {GroupId}.");
                    break;
                case RequestDeviceList deviceList:
                    Sender.Tell(new ReplyDeviceList(deviceList.RequestId, new HashSet<string>(deviceIdToActor.Keys)));
                    break;
                case Terminated t:
                    var deviceId = actorToDeviceId[t.ActorRef];
                    Log.Info($"Device actor for {deviceId} has been terminated");
                    actorToDeviceId.Remove(t.ActorRef);
                    deviceIdToActor.Remove(deviceId);
                    break;
            }
        }

        public static Props Props(string groupId) => Akka.Actor.Props.Create(() => new DeviceGroupActor(groupId));
    }
}
