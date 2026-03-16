public static class AudioID
{
    private static GameUp.Core.AudioIdentity Get(string name)
    {
        return GameUp.Core.AudioManager.TryGetIdentity(name, out var identity) ? identity : null;
    }

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
    public static GameUp.Core.AudioIdentity Skill_Skeleton_Bomb_1 => Get("Skill_Skeleton_Bomb_1");
    public static GameUp.Core.AudioIdentity Skill_Slime => Get("Skill_Slime");
    public static GameUp.Core.AudioIdentity Skill_Slime_1 => Get("Skill_Slime_1");
    public static GameUp.Core.AudioIdentity Skill_Slime_2 => Get("Skill_Slime_2");
    public static GameUp.Core.AudioIdentity Skill_Slime_3 => Get("Skill_Slime_3");
}
