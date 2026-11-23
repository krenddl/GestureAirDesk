namespace GestureFlow.Core;
public static class ScreenProjection
{
    public static (int X,int Y)? Map(double x,double y,int left,int top,int width,int height)
    {
        if(width<=0||height<=0||!double.IsFinite(x)||!double.IsFinite(y))return null;
        long px=(long)left+(long)Math.Round(Math.Clamp(x,0,1)*(width-1));
        long py=(long)top+(long)Math.Round(Math.Clamp(y,0,1)*(height-1));
        if(px<int.MinValue||px>int.MaxValue||py<int.MinValue||py>int.MaxValue)return null;
        return ((int)px,(int)py);
    }
}

