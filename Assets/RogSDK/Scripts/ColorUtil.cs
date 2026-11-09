using UnityEngine;

namespace RogPhoneSdk
{
    public static class ColorUtil
    {
        public static Color IntToColor(int colorInt, float alpha = 1f)
        {
            float baseNum = 255;

            int b = 0xFF & colorInt;
            int g = 0xFF00 & colorInt;
            g >>= 8;
            int r = 0xFF0000 & colorInt;
            r >>= 16;
            return new Color((float)r / baseNum, (float)g / baseNum, (float)b / baseNum, alpha);
        }
    }
}