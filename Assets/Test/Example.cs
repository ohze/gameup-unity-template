using UnityEngine;
using GameUp.Core;
using GameUp.Core.UI;

public class Example : MonoBehaviour
{
    public PlayerGameMainData playerGameMainData;
    private void Start()
    {
        playerGameMainData = PlayerGameMainData.Create();
        TimeUtils.Initialize();
    }

    [Button]
    public void LevelUp()
    {
        playerGameMainData.LevelUp();
    }

    [Button]
    public void TestAudio()
    {
        //AudioManager.PlayAudio(AudioID.Hit_Death);
    }

    [Button]
    public void ShowPopup()
    {
        PopupTest.OpenViewAsync();
    }

    [Button]
    public void ClosePopup()
    {
        PopupTest.CloseView();
    }

    [Button]
    public void ShowScreen()
    {
        ScreenTest.OpenViewAsync();
    }

    [Button]
    public void ShowScreen2()
    {
        ScreenTest2.OpenViewAsync();
    }

    [Button]
    public void Preload()
    {
        UIPopup.PreloadPopupByTypesAsync(typeof(PopupTest));
        UIScreen.PreloadViewByTypeAsync(typeof(ScreenTest));
    }

    [Button]
    public void Preload2()
    {
        UIPopup.PreloadPopupByTypesAsync(typeof(PopupTest));
        UIScreen.PreloadViewByTypesAsync(typeof(ScreenTest), typeof(ScreenTest2));
    }
}