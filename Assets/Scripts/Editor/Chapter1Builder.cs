#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 1챕터 기획서 내용(몬스터 6종, 행동, 수치, 이미지, 방 데이터, 방어 카드)을 에셋으로 한 번에 만든다.
///
/// - 프로젝트를 열었을 때 Assets/prefab/Chapter1 이 없으면 자동으로 한 번 실행된다.
/// - 메뉴 GreedDungeon → 1챕터 몬스터 만들기 로 언제든 다시 만들 수 있다 (같은 경로의 에셋은 값만 덮어쓴다).
///
/// 만드는 것
///  Assets/Art/Chapter1/              몬스터·아이콘 픽셀 아트 (32x32, Point 필터)
///  Assets/Resources/GreedDungeon/    부패 지대 / 위험 표시 / 균열 (런타임에 불러옴)
///  Assets/prefab/Chapter1/Actions/   행동 에셋 (공격, 방어, 소환 ...)
///  Assets/prefab/Chapter1/Selectors/ 행동 알고리즘 에셋
///  Assets/prefab/Chapter1/Prefabs/   몬스터 프리팹 (Assets/prefab/Monster.prefab 을 복사해서 만든다)
///  Assets/prefab/Chapter1/Rooms/     예시 방 데이터 (일반 3, 엘리트 2, 보스 1)
///  Assets/prefab/Carddata/방어.asset 방어 카드 (방어도 5)
///  + BossRoomData 에 몬스터가 없으면 시체왕을 넣는다
/// </summary>
[InitializeOnLoad]
public static class Chapter1Builder
{
    private const string ArtDir = "Assets/Art/Chapter1";
    private const string ResourceDir = "Assets/Resources/GreedDungeon";
    private const string RootDir = "Assets/prefab/Chapter1";
    private const string ActionDir = RootDir + "/Actions";
    private const string SelectorDir = RootDir + "/Selectors";
    private const string PrefabDir = RootDir + "/Prefabs";
    private const string RoomDir = RootDir + "/Rooms";
    private const string CardDir = "Assets/prefab/Carddata";
    private const string MonsterTemplate = "Assets/prefab/Monster.prefab";
    private const string LockDoorsTrigger = "Assets/prefab/Roomdata/LockDoorsUntilCleared.asset";
    private const string BossRoomData = "Assets/prefab/Roomdata/BossRoomData.asset";
    private const string DoneMarker = PrefabDir + "/CorpseKing.prefab";

    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

    static Chapter1Builder()
    {
        EditorApplication.delayCall += TryAutoBuild;
    }

    private static void TryAutoBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoBuild;
            return;
        }
        if (File.Exists(DoneMarker)) return;

        Debug.Log("[Chapter1Builder] 1챕터 몬스터 에셋이 없어 자동으로 만듭니다.");
        Build();
    }

    // ───────────────────────── 메뉴 ─────────────────────────

    [MenuItem("GreedDungeon/1챕터 몬스터 만들기 (다시 만들기)", priority = 0)]
    public static void Build()
    {
        Selection.activeObject = null; // Inspector 가 보던 오브젝트가 바뀌면서 나는 에러 방지
        EnsureFolder(ArtDir);
        EnsureFolder(ResourceDir);
        EnsureFolder(ActionDir);
        EnsureFolder(SelectorDir);
        EnsureFolder(PrefabDir);
        EnsureFolder(RoomDir);
        EnsureFolder(CardDir);

        WriteArtFiles();
        AssetDatabase.Refresh();
        ConfigureSprites();

        try
        {
            BuildAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Chapter1Builder] 완료: Assets/prefab/Chapter1 (몬스터 6종, 방 데이터 6개), Assets/prefab/Carddata/방어.asset");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Chapter1Builder] 만드는 중 오류: {e}");
        }
    }

    [MenuItem("GreedDungeon/1챕터 방 데이터를 Room 1~4 프리팹에 연결", priority = 20)]
    public static void LinkRoomsToPrefabs()
    {
        string[] rooms = { "Ch1_Normal_1", "Ch1_Normal_2", "Ch1_Elite_Guardian", "Ch1_Elite_Necromancer" };
        for (int i = 0; i < rooms.Length; i++)
        {
            string prefabPath = $"Assets/prefab/Room {i + 1}.prefab";
            var data = AssetDatabase.LoadAssetAtPath<RoomData>($"{RoomDir}/{rooms[i]}.asset");
            if (data == null || !File.Exists(prefabPath)) continue;

            GameObject go = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                DungeonRoom room = go.GetComponent<DungeonRoom>();
                if (room == null) continue;
                var so = new SerializedObject(room);
                Set(so, "_data", data);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Debug.Log($"[Chapter1Builder] {prefabPath} ← {rooms[i]}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(go);
            }
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("GreedDungeon/열린 씬의 시작 덱에 방어 카드 4장 추가", priority = 21)]
    public static void AddDefendCardsToDeck()
    {
        var card = AssetDatabase.LoadAssetAtPath<CardData>($"{CardDir}/방어.asset");
        if (card == null)
        {
            Debug.LogWarning("[Chapter1Builder] 방어 카드가 없습니다. 먼저 '1챕터 몬스터 만들기'를 실행하세요.");
            return;
        }

        DeckManager[] decks = Object.FindObjectsByType<DeckManager>(FindObjectsSortMode.None);
        if (decks.Length == 0)
        {
            Debug.LogWarning("[Chapter1Builder] 열린 씬에 DeckManager 가 없습니다. Main 씬을 열고 다시 실행하세요.");
            return;
        }

        foreach (DeckManager deck in decks)
        {
            var so = new SerializedObject(deck);
            SerializedProperty list = so.FindProperty("_startingDeck");
            bool found = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("card").objectReferenceValue == card)
                {
                    e.FindPropertyRelative("count").intValue = 4;
                    found = true;
                }
            }
            if (!found)
            {
                list.arraySize++;
                SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("card").objectReferenceValue = card;
                e.FindPropertyRelative("count").intValue = 4;
            }
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(deck.gameObject.scene);
            Debug.Log($"[Chapter1Builder] {deck.name} 시작 덱에 방어 x4 (씬을 저장하세요)", deck);
        }
    }

    // ───────────────────────── 이미지 ─────────────────────────

    private static void WriteArtFiles()
    {
        foreach (Chapter1ArtData.ArtEntry art in Chapter1ArtData.Entries)
        {
            string path = ArtPath(art);
            if (File.Exists(path)) continue;
            File.WriteAllBytes(path, Convert.FromBase64String(art.Base64));
        }
    }

    private static string ArtPath(Chapter1ArtData.ArtEntry art) =>
        $"{(art.Folder == "Resources" ? ResourceDir : ArtDir)}/{art.Name}.png";

    private static void ConfigureSprites()
    {
        Sprites.Clear();
        foreach (Chapter1ArtData.ArtEntry art in Chapter1ArtData.Entries)
        {
            string path = ArtPath(art);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool dirty = importer.textureType != TextureImporterType.Sprite ||
                         importer.spriteImportMode != SpriteImportMode.Single ||
                         Math.Abs(importer.spritePixelsPerUnit - 32f) > 0.01f ||
                         importer.filterMode != FilterMode.Point ||
                         importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (dirty)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) Sprites[art.Name] = sprite;
        }
    }

    private static Sprite S(string name) => Sprites.TryGetValue(name, out Sprite s) ? s : null;

    // ───────────────────────── 전체 ─────────────────────────

    private static void BuildAll()
    {
        // ── 망자의 병사
        var soldierAttack = Act<Ch1LineAttack>("Soldier_Attack", "공격", "Icon_Attack",
            F("_damage", 5), F("_minRange", 1), F("_maxRange", 2));
        var soldierHeavy = Act<Ch1FanAttack>("Soldier_HeavyAttack", "강공격", "Icon_Attack",
            F("_damage", 7));
        var soldierGuard = Act<Ch1Guard>("Soldier_Guard", "방어", "Icon_Defend",
            F("_shield", 2), F("_shieldTurns", 0), F("_armorGain", 2));
        var soldierSelector = Sel<SequenceActionSelector>("Soldier_Selector",
            F("_sequence", new Object[] { soldierAttack, soldierHeavy, soldierGuard }), F("_loop", true));

        // ── 부패한 사수
        var archerShot = Act<Ch1LineAttack>("Archer_Shot", "일반 사격", "Icon_Attack",
            F("_damage", 5), F("_minRange", 3), F("_maxRange", 5), F("_stopAtObstacles", true));
        var archerPierce = Act<Ch1LineAttack>("Archer_PierceShot", "관통 사격", "Icon_Attack",
            F("_damage", 8), F("_minRange", 1), F("_maxRange", 5), F("_stopAtObstacles", false));
        var archerDiagonal = Act<Ch1ClosestTileShot>("Archer_DiagonalShot", "대각선 사격", "Icon_Attack",
            F("_damage", 7), F("_radius", 1));
        var archerSelector = Sel<Ch1ArcherSelector>("Archer_Selector",
            F("_normalShot", archerShot), F("_pierceShot", archerPierce), F("_diagonalShot", archerDiagonal), F("_range", 5));

        // ── 역병 구울
        var ghoulClaw = Act<Ch1LineAttack>("Ghoul_Claw", "할퀴기", "Icon_Attack",
            F("_damage", 6), F("_minRange", 1), F("_maxRange", 1));
        var ghoulSpew = Act<Ch1AreaAttack>("Ghoul_RotSpew", "부패 분출", "Icon_Rot",
            F("_damage", 4), F("_center", Ch1AreaCenter.Self), F("_radius", 1), F("_rotZoneTurns", 2), F("_rotZoneDamage", 2));
        var ghoulSelector = Sel<Ch1GhoulSelector>("Ghoul_Selector",
            F("_claw", ghoulClaw), F("_spew", ghoulSpew), F("_spewMinDistance", 2), F("_spewMaxDistance", 3));

        // ── 묘지의 수호자
        var guardWall = Act<Ch1Guard>("Guardian_IronWall", "철벽 방어", "Icon_Defend",
            F("_shield", 10), F("_shieldTurns", 0), F("_armorGain", 10));
        var guardCounter = Act<Ch1Guard>("Guardian_CounterStance", "반격 태세", "Icon_Defend",
            F("_shield", 3), F("_shieldTurns", 0), F("_armorGain", 3));
        var guardBash = Act<Ch1ShieldBash>("Guardian_ShieldBash", "방패 강타", "Icon_Attack",
            F("_damage", 0), F("_rootTurns", 1), F("_knockback", 1));
        var guardSlam = Act<Ch1FanAttack>("Guardian_Slam", "내려찍기", "Icon_Attack",
            F("_damage", 12));
        var guardianSelector = Sel<Ch1GuardianSelector>("Guardian_Selector",
            F("_defenses", new Object[] { guardWall, guardCounter }), F("_shieldBash", guardBash), F("_slam", guardSlam));

        // ── 사령술사 (소환 목록은 일반 몬스터 프리팹을 만든 뒤 연결)
        var necroBolt = Act<Ch1AreaAttack>("Necromancer_DeathBolt", "사령탄", "Icon_Attack",
            F("_damage", 4), F("_centerDamage", 8), F("_center", Ch1AreaCenter.Player), F("_radius", 1),
            F("_includeCenter", true), F("_stopAtObstacles", false));
        var necroWave = Act<Ch1AreaAttack>("Necromancer_DeathWave", "죽음의 파동", "Icon_Attack",
            F("_damage", 6), F("_center", Ch1AreaCenter.Self), F("_radius", 2));
        var necroDrain = Act<Ch1LineAttack>("Necromancer_LifeDrain", "생명력 흡수", "Icon_Attack",
            F("_damage", 5), F("_minRange", 1), F("_maxRange", 3), F("_lifeSteal", true));
        var necroSummon = Act<Ch1Summon>("Necromancer_Summon", "망자 소환", "Icon_Summon",
            F("_maxAlive", 2));
        var necroSelector = Sel<Ch1NecromancerSelector>("Necromancer_Selector",
            F("_bag", new Object[] { necroBolt, necroWave }), F("_lifeDrain", necroDrain), F("_drainRange", 3), F("_summon", necroSummon));

        // ── 시체왕 1페이즈
        var kingSmash = Act<Ch1LineAttack>("CorpseKing_BodySlam", "육체 강타", "Icon_Attack",
            F("_damage", 10), F("_minRange", 1), F("_maxRange", 3), F("_fromNearestBodyTile", true));
        var kingCrush = Act<Ch1AreaAttack>("CorpseKing_CorpseCrush", "시체 분쇄", "Icon_Attack",
            F("_damage", 12), F("_center", Ch1AreaCenter.Self), F("_radius", 1));
        var kingRotWave = Act<Ch1AreaAttack>("CorpseKing_RotWave", "부패의 파동", "Icon_Rot",
            F("_damage", 0), F("_center", Ch1AreaCenter.Self), F("_radius", 2), F("_decayTurns", 2), F("_decayDamage", 2));
        var kingCollapse = Act<Ch1DelayedArea>("CorpseKing_GroundCollapse", "대지 붕괴", "Icon_Attack",
            F("_damage", 10), F("_radius", 1), F("_center", Ch1AreaCenter.NearestBodyTile));
        var kingPhase1 = Sel<Ch1AlternatingSelector>("CorpseKing_Phase1_Selector",
            F("_groupA", new Object[] { kingSmash, kingCrush }), F("_groupB", new Object[] { kingRotWave, kingCollapse }));

        // ── 시체왕 2페이즈
        var kingCharge = Act<Ch1LineAttack>("CorpseKing_Charge", "육체 돌진", "Icon_Attack",
            F("_damage", 12), F("_minRange", 1), F("_maxRange", 3), F("_moveTowardAfter", 2));
        var kingBarrage = Act<Ch1AreaAttack>("CorpseKing_Barrage", "난타", "Icon_Attack",
            F("_damage", 5), F("_hits", 2), F("_center", Ch1AreaCenter.Self), F("_radius", 1), F("_diamond", true));
        var kingBurst = Act<Ch1AreaAttack>("CorpseKing_RotBurst", "부패 폭발", "Icon_Rot",
            F("_damage", 10), F("_center", Ch1AreaCenter.Self), F("_radius", 1), F("_decayTurns", 2), F("_decayDamage", 2));
        var kingRage = Act<Ch1Rage>("CorpseKing_Rage", "광폭화", "Icon_Buff",
            F("_amount", 3), F("_turns", 3));
        var kingPhase2 = Sel<Ch1AlternatingSelector>("CorpseKing_Phase2_Selector",
            F("_groupA", new Object[] { kingCharge, kingBarrage }), F("_groupB", new Object[] { kingBurst, kingRage }));
        var kingSelector = Sel<Ch1CorpseKingSelector>("CorpseKing_Selector",
            F("_phase1", kingPhase1), F("_phase2", kingPhase2));

        // ── 프리팹
        GameObject soldier = MakeMonster("UndeadSoldier", "망자의 병사", "Undead_Soldier", 25, soldierSelector, EnemyRank.Normal,
            EnemyPostMove.Chase, null);
        GameObject archer = MakeMonster("RottenArcher", "부패한 사수", "Rotten_Archer", 18, archerSelector, EnemyRank.Normal,
            EnemyPostMove.KeepDistance, null);
        GameObject ghoul = MakeMonster("PlagueGhoul", "역병 구울", "Plague_Ghoul", 32, ghoulSelector, EnemyRank.Normal,
            EnemyPostMove.Chase, null);

        Edit(necroSummon, so => Set(so, "_prefabs", new Object[] { soldier, archer, ghoul }));

        GameObject guardian = MakeMonster("GraveyardGuardian", "묘지의 수호자", "Graveyard_Guardian", 75, guardianSelector, EnemyRank.Elite,
            EnemyPostMove.Chase, null);
        GameObject necromancer = MakeMonster("Necromancer", "사령술사", "Necromancer", 55, necroSelector, EnemyRank.Elite,
            EnemyPostMove.Chase, go =>
            {
                if (go.GetComponent<KillMinionsOnDeath>() == null) go.AddComponent<KillMinionsOnDeath>();
                EnemyController ec = go.GetComponent<EnemyController>();
                Edit(ec, so => Set(so, "_nearDistance", 2));
            });
        GameObject corpseKing = MakeMonster("CorpseKing", "시체왕", "CorpseKing_Core", 180, kingSelector, EnemyRank.Boss,
            EnemyPostMove.None, go =>
            {
                CorpseKingBoss boss = go.GetComponent<CorpseKingBoss>();
                if (boss == null) boss = go.AddComponent<CorpseKingBoss>();
                Edit(boss, so =>
                {
                    Set(so, "_bodyTileSprite", S("CorpseKing_Body"));
                    Set(so, "_coreSprite", S("CorpseKing_Core"));
                    Set(so, "_revealedSprite", S("CorpseKing_Revealed"));
                    Set(so, "_phase1HealthRatio", 0.5f);
                    Set(so, "_corpseTileCount", 10);
                });
                EnemyController ec = go.GetComponent<EnemyController>();
                Edit(ec, so => Set(so, "_isBoss", true));
            });

        // ── 방 데이터
        Room("Ch1_Normal_1", RoomType.Normal, (soldier, new Vector2Int(3, 4)), (soldier, new Vector2Int(7, 2)), (archer, new Vector2Int(8, 5)));
        Room("Ch1_Normal_2", RoomType.Normal, (ghoul, new Vector2Int(4, 4)), (soldier, new Vector2Int(6, 2)), (archer, new Vector2Int(9, 5)));
        Room("Ch1_Normal_3", RoomType.Normal, (ghoul, new Vector2Int(3, 2)), (ghoul, new Vector2Int(7, 4)), (archer, new Vector2Int(9, 1)));
        Room("Ch1_Elite_Guardian", RoomType.Elite, (guardian, new Vector2Int(5, 4)), (archer, new Vector2Int(8, 5)));
        Room("Ch1_Elite_Necromancer", RoomType.Elite, (necromancer, new Vector2Int(6, 4)));
        Room("Ch1_Boss_CorpseKing", RoomType.Boss, (corpseKing, new Vector2Int(5, 3)));
        AddBossToBossRoom(corpseKing);

        // ── 방어 카드 (이미 있으면 건드리지 않는다: donghyeok 카드 목록의 방어 카드를 시작 덱/보상에서 쓰고 있음)
        if (AssetDatabase.LoadAssetAtPath<CardData>($"{CardDir}/방어.asset") != null) return;
        var defendEffect = Asset<DefendCardEffect>($"{CardDir}/DefendCardEffect_5.asset", so => Set(so, "_block", 5));
        Asset<CardData>($"{CardDir}/방어.asset", so =>
        {
            Set(so, "_cardName", "방어");
            Set(so, "_cost", 1);
            Set(so, "_artwork", S("Icon_Defend"));
            Set(so, "_description", "방어도 5를 얻습니다.\n(다음 내 턴 시작 시 사라짐)");
            Set(so, "_effects", new Object[] { defendEffect });
        });
    }

    // ───────────────────────── 에셋 도우미 ─────────────────────────

    private readonly struct Field
    {
        public readonly string Key;
        public readonly object Value;

        public Field(string key, object value)
        {
            Key = key;
            Value = value;
        }
    }

    private static Field F(string key, object value) => new Field(key, value);

    private static T Act<T>(string file, string displayName, string icon, params Field[] fields) where T : EnemyAction
    {
        return Asset<T>($"{ActionDir}/{file}.asset", so =>
        {
            Set(so, "_displayName", displayName);
            Set(so, "_icon", S(icon));
            Set(so, "_showValue", true);
            foreach (Field f in fields) Set(so, f.Key, f.Value);
        });
    }

    private static T Sel<T>(string file, params Field[] fields) where T : EnemyActionSelector
    {
        return Asset<T>($"{SelectorDir}/{file}.asset", so =>
        {
            foreach (Field f in fields) Set(so, f.Key, f.Value);
        });
    }

    private static T Asset<T>(string path, Action<SerializedObject> setup) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        Edit(asset, setup);
        return asset;
    }

    private static void Edit(Object target, Action<SerializedObject> setup)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        setup(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void Set(SerializedObject so, string name, object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning($"[Chapter1Builder] {so.targetObject.GetType().Name} 에 '{name}' 필드가 없습니다.");
            return;
        }

        if (value == null) p.objectReferenceValue = null;
        else if (value is Enum e)
        {
            if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = Convert.ToInt32(e);
            else p.intValue = Convert.ToInt32(e);
        }
        else if (value is int i) p.intValue = i;
        else if (value is bool b) p.boolValue = b;
        else if (value is float f) p.floatValue = f;
        else if (value is string s) p.stringValue = s;
        else if (value is Vector2Int v) p.vector2IntValue = v;
        else if (value is Color c) p.colorValue = c;
        else if (value is Object o) p.objectReferenceValue = o;
        else if (value is IList list)
        {
            p.arraySize = list.Count;
            for (int k = 0; k < list.Count; k++)
                p.GetArrayElementAtIndex(k).objectReferenceValue = list[k] as Object;
        }
        else Debug.LogWarning($"[Chapter1Builder] '{name}' 에 넣을 수 없는 값: {value}");
    }

    // ───────────────────────── 프리팹 ─────────────────────────

    private static GameObject MakeMonster(string file, string displayName, string sprite, int health,
                                          EnemyActionSelector selector, EnemyRank rank, EnemyPostMove postMove,
                                          Action<GameObject> extra)
    {
        string path = $"{PrefabDir}/{file}.prefab";
        GameObject go = File.Exists(MonsterTemplate) ? PrefabUtility.LoadPrefabContents(MonsterTemplate) : CreateBareMonster();

        try
        {
            go.name = file;

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = S(sprite);
            sr.color = Color.white;

            EnemyHealth hp = go.GetComponent<EnemyHealth>();
            if (hp == null) hp = go.AddComponent<EnemyHealth>();
            Edit(hp, so => Set(so, "_maxHealth", health));

            if (go.GetComponent<DamageShield>() == null) go.AddComponent<DamageShield>();

            EnemyController ec = go.GetComponent<EnemyController>();
            if (ec == null) ec = go.AddComponent<EnemyController>();
            Edit(ec, so =>
            {
                Set(so, "_selector", selector);
                Set(so, "_rank", rank);
                Set(so, "_postMove", postMove);
                Set(so, "_chaseSteps", 1);
                Set(so, "_nearDistance", 1);
                Set(so, "_retreatDistance", 3);
                Set(so, "_retreatSteps", 2);
                Set(so, "_isBoss", false);
                Set(so, "_bodySprite", sr);
            });

            extra?.Invoke(go);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Debug.Log($"[Chapter1Builder] {displayName} → {path}");
            return saved;
        }
        finally
        {
            if (File.Exists(MonsterTemplate)) PrefabUtility.UnloadPrefabContents(go);
            else Object.DestroyImmediate(go);
        }
    }

    /// <summary>Monster.prefab 이 없을 때 쓰는 최소 구성 (예고 아이콘/체력 표시는 없음)</summary>
    private static GameObject CreateBareMonster()
    {
        Debug.LogWarning($"[Chapter1Builder] {MonsterTemplate} 이 없어 최소 구성 몬스터로 만듭니다 (예고 아이콘·체력 표시 없음).");
        var go = new GameObject("Monster");
        go.AddComponent<SpriteRenderer>().sortingOrder = 3;
        go.AddComponent<EnemyHealth>();
        go.AddComponent<DamageShield>();
        go.AddComponent<EnemyController>();
        return go;
    }

    // ───────────────────────── 방 ─────────────────────────

    private static void Room(string file, RoomType type, params (GameObject prefab, Vector2Int cell)[] spawns)
    {
        Object lockDoors = AssetDatabase.LoadAssetAtPath<Object>(LockDoorsTrigger);
        Asset<RoomData>($"{RoomDir}/{file}.asset", so =>
        {
            Set(so, "_roomType", type);
            Set(so, "_clearedOnEnter", false);

            SerializedProperty list = so.FindProperty("_spawns");
            list.arraySize = spawns.Length;
            for (int i = 0; i < spawns.Length; i++)
                WriteSpawn(list.GetArrayElementAtIndex(i), spawns[i].prefab, spawns[i].cell);

            Set(so, "_enterTriggers", lockDoors != null ? new Object[] { lockDoors } : new Object[0]);
        });
    }

    private static void WriteSpawn(SerializedProperty e, GameObject prefab, Vector2Int cell)
    {
        e.FindPropertyRelative("kind").enumValueIndex = (int)RoomSpawnKind.Monster;
        e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        e.FindPropertyRelative("cell").vector2IntValue = cell;
        e.FindPropertyRelative("blocksMovement").boolValue = true;
        e.FindPropertyRelative("fitToCell").boolValue = true;
        e.FindPropertyRelative("fillRatio").floatValue = 0.9f;
    }

    /// <summary>기존 BossRoomData 에 몬스터가 하나도 없으면 시체왕을 가운데에 넣는다</summary>
    private static void AddBossToBossRoom(GameObject corpseKing)
    {
        var data = AssetDatabase.LoadAssetAtPath<RoomData>(BossRoomData);
        if (data == null || corpseKing == null) return;

        var so = new SerializedObject(data);
        SerializedProperty list = so.FindProperty("_spawns");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("kind").enumValueIndex == (int)RoomSpawnKind.Monster)
                return; // 이미 몬스터가 있으면 건드리지 않는다

        list.arraySize++;
        WriteSpawn(list.GetArrayElementAtIndex(list.arraySize - 1), corpseKing, new Vector2Int(5, 3));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        Debug.Log("[Chapter1Builder] BossRoomData 에 시체왕 배치 (5, 3)");
    }

    // ───────────────────────── 폴더 ─────────────────────────

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
