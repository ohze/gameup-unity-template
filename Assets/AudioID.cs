#if UNITY_EDITOR
public static class AudioID
{
    private static T Get<T>(string path, ref T field) where T : UnityEngine.Object {
        if (field == null) field = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        return field;
    }

    private static GameUp.Core.AudioIdentity _hit_death;
    public static GameUp.Core.AudioIdentity Hit_Death => Get("Assets/Game/Data/Hit_Death.asset", ref _hit_death);

    private static GameUp.Core.AudioIdentity _hit_demon_lord;
    public static GameUp.Core.AudioIdentity Hit_Demon_Lord => Get("Assets/Game/Data/Hit_Demon_Lord.asset", ref _hit_demon_lord);

    private static GameUp.Core.AudioIdentity _hit_medusa;
    public static GameUp.Core.AudioIdentity Hit_Medusa => Get("Assets/Game/Data/Hit_Medusa.asset", ref _hit_medusa);

    private static GameUp.Core.AudioIdentity _hit_mimic;
    public static GameUp.Core.AudioIdentity Hit_Mimic => Get("Assets/Game/Data/Hit_Mimic.asset", ref _hit_mimic);

    private static GameUp.Core.AudioIdentity _hit_necromancer;
    public static GameUp.Core.AudioIdentity Hit_Necromancer => Get("Assets/Game/Data/Hit_Necromancer.asset", ref _hit_necromancer);

    private static GameUp.Core.AudioIdentity _hit_skeleton;
    public static GameUp.Core.AudioIdentity Hit_Skeleton => Get("Assets/Game/Data/Hit_Skeleton.asset", ref _hit_skeleton);

    private static GameUp.Core.AudioIdentity _hit_skeleton_bomb;
    public static GameUp.Core.AudioIdentity Hit_Skeleton_Bomb => Get("Assets/Game/Data/Hit_Skeleton_Bomb.asset", ref _hit_skeleton_bomb);

    private static GameUp.Core.AudioIdentity _hit_slime;
    public static GameUp.Core.AudioIdentity Hit_Slime => Get("Assets/Game/Data/Hit_Slime.asset", ref _hit_slime);

    private static GameUp.Core.AudioIdentity _skill_death;
    public static GameUp.Core.AudioIdentity Skill_Death => Get("Assets/Game/Data/Skill_Death.asset", ref _skill_death);

    private static GameUp.Core.AudioIdentity _skill_demon_lord_cast;
    public static GameUp.Core.AudioIdentity Skill_Demon_Lord_Cast => Get("Assets/Game/Data/Skill_Demon_Lord_Cast.asset", ref _skill_demon_lord_cast);

    private static GameUp.Core.AudioIdentity _skill_medusa;
    public static GameUp.Core.AudioIdentity Skill_Medusa => Get("Assets/Game/Data/Skill_Medusa.asset", ref _skill_medusa);

    private static GameUp.Core.AudioIdentity _skill_mimic;
    public static GameUp.Core.AudioIdentity Skill_Mimic => Get("Assets/Game/Data/Skill_Mimic.asset", ref _skill_mimic);

    private static GameUp.Core.AudioIdentity _skill_necromancer;
    public static GameUp.Core.AudioIdentity Skill_Necromancer => Get("Assets/Game/Data/Skill_Necromancer.asset", ref _skill_necromancer);

    private static GameUp.Core.AudioIdentity _skill_skeleton;
    public static GameUp.Core.AudioIdentity Skill_Skeleton => Get("Assets/Game/Data/Skill_Skeleton.asset", ref _skill_skeleton);

    private static GameUp.Core.AudioIdentity _skill_skeleton_bomb;
    public static GameUp.Core.AudioIdentity Skill_Skeleton_Bomb => Get("Assets/Game/Data/Skill_Skeleton_Bomb.asset", ref _skill_skeleton_bomb);

    private static GameUp.Core.AudioIdentity _skill_skeleton_bomb_1;
    public static GameUp.Core.AudioIdentity Skill_Skeleton_Bomb_1 => Get("Assets/Game/Data/Skill_Skeleton_Bomb_1.asset", ref _skill_skeleton_bomb_1);

    private static GameUp.Core.AudioIdentity _skill_slime;
    public static GameUp.Core.AudioIdentity Skill_Slime => Get("Assets/Game/Data/Skill_Slime.asset", ref _skill_slime);

    private static GameUp.Core.AudioIdentity _skill_slime_1;
    public static GameUp.Core.AudioIdentity Skill_Slime_1 => Get("Assets/Game/Data/Skill_Slime_1.asset", ref _skill_slime_1);

    private static GameUp.Core.AudioIdentity _skill_slime_2;
    public static GameUp.Core.AudioIdentity Skill_Slime_2 => Get("Assets/Game/Data/Skill_Slime_2.asset", ref _skill_slime_2);

    private static GameUp.Core.AudioIdentity _skill_slime_3;
    public static GameUp.Core.AudioIdentity Skill_Slime_3 => Get("Assets/Game/Data/Skill_Slime_3.asset", ref _skill_slime_3);

}
#endif
