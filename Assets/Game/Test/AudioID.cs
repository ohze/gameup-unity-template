public static class AudioID
{
    private static GameUp.Core.AudioIdentity Get(string name)
    {
        return GameUp.Core.AudioManager.TryGetIdentity(name, out var identity) ? identity : null;
    }

    public static GameUp.Core.AudioIdentity Hero_Hit_Mage => Get("Hero_Hit_Mage");
    public static GameUp.Core.AudioIdentity Hero_Hit_Priest => Get("Hero_Hit_Priest");
    public static GameUp.Core.AudioIdentity Hero_Hit_Ranger => Get("Hero_Hit_Ranger");
    public static GameUp.Core.AudioIdentity Hero_Hit_Shielder => Get("Hero_Hit_Shielder");
    public static GameUp.Core.AudioIdentity Hero_Hit_Thief => Get("Hero_Hit_Thief");
    public static GameUp.Core.AudioIdentity Hero_Hit_Warrior => Get("Hero_Hit_Warrior");
    public static GameUp.Core.AudioIdentity Hero_Skill_Mage => Get("Hero_Skill_Mage");
    public static GameUp.Core.AudioIdentity Hero_Skill_Priest => Get("Hero_Skill_Priest");
    public static GameUp.Core.AudioIdentity Hero_Skill_Ranger => Get("Hero_Skill_Ranger");
    public static GameUp.Core.AudioIdentity Hero_Skill_Shielder => Get("Hero_Skill_Shielder");
    public static GameUp.Core.AudioIdentity Hero_Skill_Thief => Get("Hero_Skill_Thief");
    public static GameUp.Core.AudioIdentity Hit_Death => Get("Hit_Death");
    public static GameUp.Core.AudioIdentity Hit_Demon_Lord => Get("Hit_Demon_Lord");
    public static GameUp.Core.AudioIdentity Hit_Medusa => Get("Hit_Medusa");
    public static GameUp.Core.AudioIdentity Hit_Mimic => Get("Hit_Mimic");
    public static GameUp.Core.AudioIdentity Hit_Necromancer => Get("Hit_Necromancer");
    public static GameUp.Core.AudioIdentity Hit_Skeleton => Get("Hit_Skeleton");
    public static GameUp.Core.AudioIdentity Hit_Skeleton_Bomb => Get("Hit_Skeleton_Bomb");
    public static GameUp.Core.AudioIdentity Hit_Slime => Get("Hit_Slime");
    public static GameUp.Core.AudioIdentity Skill_Death => Get("Skill_Death");
    public static GameUp.Core.AudioIdentity Skill_Demon_Lord_Cast => Get("Skill_Demon_Lord_Cast");
    public static GameUp.Core.AudioIdentity Skill_Medusa => Get("Skill_Medusa");
    public static GameUp.Core.AudioIdentity Skill_Mimic => Get("Skill_Mimic");
    public static GameUp.Core.AudioIdentity Skill_Necromancer => Get("Skill_Necromancer");
    public static GameUp.Core.AudioIdentity Skill_Skeleton => Get("Skill_Skeleton");
    public static GameUp.Core.AudioIdentity Skill_Skeleton_Bomb => Get("Skill_Skeleton_Bomb");
    public static GameUp.Core.AudioIdentity Skill_Slime => Get("Skill_Slime");
}
