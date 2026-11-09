using RogPhoneSdk;

namespace RogPhoneSdkDemo
{
    public interface IDeviceConnectionObserver
    {
        void NotifyConnected(DeviceInfo gamepadInfo);
        void NotifyDisconnected(DeviceInfo gamepadInfo);
    }

}