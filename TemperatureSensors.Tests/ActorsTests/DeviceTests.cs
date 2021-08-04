using Akka.Actor;
using Akka.TestKit.Xunit2;
using FluentAssertions;
using System;
using TemperatureSensors.Actors;
using TemperatureSensors.Messages;
using Xunit;

namespace TemperatureSensors.Tests.ActorsTests
{
    public class DeviceTests : TestKit
    {
        [Fact]
        public void Device_actor_must_reply_with_empty_reading_if_no_temperature_is_known()
        {
            var probe = CreateTestProbe();
            var deviceActor = Sys.ActorOf(DeviceActor.Props("group", "device"));

            deviceActor.Tell(new ReadTemperature(requestId: 42), probe.Ref);
            var response = probe.ExpectMsg<RespondTemperature>();
            response.RequestId.Should().Be(42);
            response.Value.Should().Be(null);
        }

        [Fact]
        public void Device_actor_must_reply_with_latest_temperature_reading()
        {
            var probe = CreateTestProbe();
            var deviceActor = Sys.ActorOf(DeviceActor.Props("group", "device"));

            deviceActor.Tell(new RecordTemperature(requestId: 1, value: 24.0), probe.Ref);
            probe.ExpectMsg<TemperatureRecorded>(s => s.RequestId == 1);

            deviceActor.Tell(new ReadTemperature(requestId: 2), probe.Ref);
            var response1 = probe.ExpectMsg<RespondTemperature>();
            response1.RequestId.Should().Be(2);
            response1.Value.Should().Be(24.0);

            deviceActor.Tell(new RecordTemperature(requestId: 3, value: 55.0), probe.Ref);
            probe.ExpectMsg<TemperatureRecorded>(s => s.RequestId == 3);

            deviceActor.Tell(new ReadTemperature(requestId: 4), probe.Ref);
            var response2 = probe.ExpectMsg<RespondTemperature>();
            response2.RequestId.Should().Be(4);
            response2.Value.Should().Be(55.0);
        }

        [Fact]
        public void Device_actor_must_reply_to_registration_requests()
        {
            var probe = CreateTestProbe();
            var deviceActor = Sys.ActorOf(DeviceActor.Props("group", "device"));

            deviceActor.Tell(new RequestTrackDevice("group", "device"), probe.Ref);
            probe.ExpectMsg<DeviceRegistered>();
            probe.LastSender.Should().Be(deviceActor);
        }

        [Fact]
        public void Device_actor_must_ignore_wrong_registration_requests()
        {
            var probe = CreateTestProbe();
            var deviceActor = Sys.ActorOf(DeviceActor.Props("group", "device"));

            deviceActor.Tell(new RequestTrackDevice("wrongGroup", "device"), probe.Ref);
            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(500));

            deviceActor.Tell(new RequestTrackDevice("group", "Wrongdevice"), probe.Ref);
            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        }

        [Fact]
        public void DeviceGroup_actor_must_be_able_to_list_active_devices()
        {
            var probe = CreateTestProbe();
            var groupActor = Sys.ActorOf(DeviceGroupActor.Props("group"));

            groupActor.Tell(new RequestTrackDevice("group", "device1"), probe.Ref);
            probe.ExpectMsg<DeviceRegistered>();

            groupActor.Tell(new RequestTrackDevice("group", "device2"), probe.Ref);
            probe.ExpectMsg<DeviceRegistered>();

            groupActor.Tell(new RequestDeviceList(requestId: 0), probe.Ref);
            probe.ExpectMsg<ReplyDeviceList>(s => s.RequestId == 0
                && s.Ids.Contains("device1")
                && s.Ids.Contains("device2"));
        }

        [Fact]
        public void DeviceGroup_actor_must_be_able_to_list_active_devices_after_one_shuts_down()
        {
            var probe = CreateTestProbe();
            var groupActor = Sys.ActorOf(DeviceGroupActor.Props("group"));

            groupActor.Tell(new RequestTrackDevice("group", "device1"), probe.Ref);
            probe.ExpectMsg<DeviceRegistered>();
            var toShutDown = probe.LastSender;

            groupActor.Tell(new RequestTrackDevice("group", "device2"), probe.Ref);
            probe.ExpectMsg<DeviceRegistered>();

            groupActor.Tell(new RequestDeviceList(requestId: 0), probe.Ref);
            probe.ExpectMsg<ReplyDeviceList>(s => s.RequestId == 0
                                                  && s.Ids.Contains("device1")
                                                  && s.Ids.Contains("device2"));

            probe.Watch(toShutDown);
            toShutDown.Tell(PoisonPill.Instance);
            probe.ExpectTerminated(toShutDown);

            // using awaitAssert to retry because it might take longer for the groupActor
            // to see the Terminated, that order is undefined
            probe.AwaitAssert(() =>
            {
                groupActor.Tell(new RequestDeviceList(requestId: 1), probe.Ref);
                probe.ExpectMsg<ReplyDeviceList>(s => s.RequestId == 1 && s.Ids.Contains("device2"));
            });
        }
    }
}
