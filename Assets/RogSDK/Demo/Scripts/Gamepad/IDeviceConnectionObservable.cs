namespace RogPhoneSdkDemo
{
    public interface IDeviceConnectionObservable
    {
        void AddConnectionObserver(IDeviceConnectionObserver observer);
        void RemoveConnectionObserver(IDeviceConnectionObserver observer);
    }

}