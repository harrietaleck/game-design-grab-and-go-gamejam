namespace RogPhoneSdk
{
    public class DeviceInfo
    {
        public GamepadEnum gamepad;
        public int deviceId;
        public DeviceInfo(string connectionInfo)
        {
            string[] args = connectionInfo.Split('/');
            gamepad = (GamepadEnum)int.Parse(args[0]);
            deviceId = int.Parse(args[1]);
        }
    }
}