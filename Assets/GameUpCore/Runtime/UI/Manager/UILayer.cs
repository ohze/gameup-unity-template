namespace GameUp.UI
{
    /// <summary>
    /// Defines UI sorting layers. Higher values render on top.
    /// </summary>
    public enum UILayer
    {
        Background = 0,
        Screen = 100,
        Popup = 200,
        Overlay = 300,
        Toast = 400
    }
}
