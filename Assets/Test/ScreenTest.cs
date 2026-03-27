using GameUp.Core.UI;
using GameUp.Core;
public class ScreenTest : UIScreen<ScreenTest>
{
    public override void OnOpen()
    {
        base.OnOpen();
        GULogger.Log("ScreenTest OnOpen");
    }
}