using GameUp.Core.UI;
using GameUp.Core;
public class ScreenTest2 : UIScreen<ScreenTest2>
{
    public override void OnOpen()
    {
        base.OnOpen();
        GULogger.Log("ScreenTest OnOpen");
    }
}