using System.Collections.Generic;
using System;
using GameUp.Core;

[Serializable]
public class PlayerGameMainData : BaseDataSave<PlayerGameMainData>
{
    public int CurrentLevel;
    public int CurrentMission;
    public bool isUnlockedLeaderHero = false;
    public List<int> unlockHeroList = new List<int>();
    public Dictionary<int, bool> unlockHeroLevelList = new Dictionary<int, bool>();
    protected override void InitDefault()
    {
        CurrentLevel = 0;
        CurrentMission = 0;

        isUnlockedLeaderHero = false;
        unlockHeroList.Clear();

        unlockHeroLevelList.Clear();
        for (int i = 0; i < 10; i++)
        {
            unlockHeroLevelList.Add(i, false);
        }
    }


    protected override void InitHasKey()
    {

    }

    public void MissionUp(int levelPlay)
    {
        //if (levelPlay < CurrentLevel) return;
        //CurrentMission++;
        //if (CurrentMission > SOEnemyData.Instance.perLoopThemeEnemy)
        //{
        //    CurrentMission = 0;
        //    CurrentLevel++;
        //}
        //Save();
    }


    public void LevelUp(int levelPlay)
    {
        CurrentLevel++;
        Save();

    }

    public void LevelUp()
    {
        GULogger.Log("PlayerGameMainData", "LevelUp");
        CurrentLevel++;
        Save();
    }

    public void LevelChange(int levelChange)
    {
        CurrentLevel = levelChange;
        Save();
    }

    public void UnlockLeaderHero()
    {
        if (isUnlockedLeaderHero)
            return;

        isUnlockedLeaderHero = true;
        Save();

        //OnUnlockLeaderHero.Dispatch();
    }

    // public void UnlockHero(int heroId)
    // {
    //     if (unlockHeroList.Contains(heroId))
    //         return;
    //
    //     unlockHeroList.Add(heroId);
    //     
    //     Save();
    // }

    public void UnlockHeroes(IEnumerable<int> heroIds)
    {
        bool changed = false;

        foreach (var id in heroIds)
        {
            if (unlockHeroList.Contains(id))
                continue;

            unlockHeroList.Add(id);
            changed = true;

            if (id == 0)
                isUnlockedLeaderHero = true;
        }

        if (changed)
            Save();
    }
}