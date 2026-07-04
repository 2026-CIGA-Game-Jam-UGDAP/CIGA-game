using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 一次性工具：生成/更新所有关卡的 DialogueSO + CharacterConfig 资产。
/// 菜单：Tools → 生成所有对话资产
/// </summary>
public static class CreateAllDialogues
{
    // 角色定义：(名字, spriteSheet中sprite名, isLeftSide)
    static readonly (string name, string spriteName, bool isLeft)[] CHARACTERS =
    {
        ("方波", "dog",  true),
        ("拉兹", "cat",  false),
    };

    const string SPRITE_SHEET_PATH = "Assets/Art/output.lin (2).png";
    const string CHAR_CONFIG_DIR = "Assets/data/CharacterConfigs";
    const string DIALOGUE_DIR = "Assets/data/Dialogues";

    [MenuItem("Tools/生成所有对话资产")]
    public static void CreateAll()
    {
        // 确保目录
        EnsureDir(CHAR_CONFIG_DIR);
        EnsureDir(DIALOGUE_DIR);

        // 1. 生成 CharacterConfig 资产 + 建立 name→config 映射
        Dictionary<string, CharacterConfig> configMap = CreateCharacterConfigs();

        // 2. 生成所有对话
        CreateDialogues(configMap);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateAllDialogues] ✅ 全部对话资产 + 角色配置已生成");
    }

    // ==================== CharacterConfig ====================

    static Dictionary<string, CharacterConfig> CreateCharacterConfigs()
    {
        // 加载 sprite sheet 里的所有 sprite
        var allSprites = AssetDatabase.LoadAllAssetsAtPath(SPRITE_SHEET_PATH)
            .OfType<Sprite>()
            .ToArray();

        var map = new Dictionary<string, CharacterConfig>();

        foreach (var (name, spriteName, isLeft) in CHARACTERS)
        {
            string path = $"{CHAR_CONFIG_DIR}/Character_{name}.asset";

            // 查找或创建
            CharacterConfig cfg = AssetDatabase.LoadAssetAtPath<CharacterConfig>(path);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<CharacterConfig>();
                AssetDatabase.CreateAsset(cfg, path);
            }

            cfg.characterName = name;
            cfg.smallPortrait = null; // 暂无小头像素材
            cfg.bigPortrait = allSprites.FirstOrDefault(s => s.name == spriteName);
            cfg.isLeftSide = isLeft;

            EditorUtility.SetDirty(cfg);
            map[name] = cfg;

            Debug.Log($"  🧑 {name} → bigPortrait={(cfg.bigPortrait != null ? spriteName : "❌未找到")}");
        }

        return map;
    }

    // ==================== Dialogues ====================

    static void CreateDialogues(Dictionary<string, CharacterConfig> configMap)
    {
        // ============ 开场旁白（新游戏） ============
        Create("Opening", true,
            ("", "20xx年，巨行星的轨道再次发生迁移，带来了一场轰轰烈烈的由彗星和小行星碎片组成的陨石雨。"),
            ("", "大量、快速的陨石撞击对地球是毁灭性的。人类便放弃了已经被陨石摧毁的地球，也遗弃了环绕在地球附近的资产。"),
            ("", "废弃的空间站、人造卫星、奇形怪状的飞船……人类的探究吸引了一大批宇宙探险家前来探险、收集资料，有些星球甚至称其为\"伟大的文明复兴计划\"……"),
            ("", "……只不过，伟大的探索总是伴随着一定的风险……"),
            ("", "比如两个被小陨石击碎探索梦、看起来只能飘在宇宙里的倒霉蛋……")
        );

        // ============ 关卡0：进入开放空间站平台 ============
        Create("Level0_Enter", false,
            ("方波", "这里是……被砸开的人类空间站？"),
            ("方波", "拉兹，我们的飞船是不是……彻底被陨星撞碎成渣了？"),
            ("拉兹", "嗯。"),
            ("方波", "还有什么补救的机会吧？比如……"),
            ("拉兹", "方波，我可以用最直白、最不绕弯子、最体贴、最符合你情感预期的方式告诉你——死心吧。"),
            ("方波", "唉……不过，起码我们终于不用飘着，能在这空间站先歇歇脚，再想下一步……"),
            ("拉兹", "呃……提醒你一下，比起说是我们\"找到\"它，更像是它\"找到\"我们。"),
            ("拉兹", "我们脚下的钉鞋现在被这平台吸附，跳都跳不起来……就算长了三对鸡翅也飞不起来。"),
            ("方波", "我记得当时买这装备的时候，店员说它能紧紧吸附在所有的星体和平台上……"),
            ("方波", "然后，如果想解除吸附的话，只需要……"),
            ("", "按住【Lshift/Enter】脱离对空间站的吸附！")
        );

        // ============ 关卡0：两个玩家解除吸附后 ============
        Create("Level0_AfterDetach", false,
            ("拉兹", "记性真好。那如果想要回到平台上，对其进行吸附，应该只需要……"),
            ("", "再次按住【Lshift/Enter】进行对空间站的吸附！")
        );

        // ============ 关卡0：两个玩家吸附平台后 ============
        Create("Level0_AfterAttach", false,
            ("方波", "没错！但……如果之后要离开这里，我们还需要一个能把我们往前推的工具……"),
            ("拉兹", "比如……一个喷气背包？"),
            ("方波", "……什么？"),
            ("拉兹", "我刚刚对这里做了一下简单的侦查，在地下发现了两个备用的、全新的喷气背包。"),
            ("拉兹", "这应该是人类留下的，起码有了这个我们行动起来就方便多了。"),
            ("拉兹", "至于它的用法……看见墙上这些图案了吗？我猜它是操作手册。"),
            ("方波", "所以我们要先按这些键，然后就能飞起来……"),
            ("拉兹", "不过似乎要先保证我们没有吸附在平台上，否则背包不会起作用。"),
            ("方波", "你还记得怎么做是取消吸附吧？")
        );

        // ============ 关卡0：两个玩家解除吸附（准备用喷气背包） ============
        Create("Level0_AfterDetach2", false,
            ("", "可以用WASD/左上右控制喷气背包的方向！"),
            ("", "在使用背包前，记得要解除自身的吸附哦！")
        );

        // ============ 关卡0：两个玩家成功启动喷气背包 ============
        Create("Level0_AfterJetpack", false,
            ("方波", "人类还发明过这么方便的东西？我还以为他们笨到只能往宇宙里扔违章建筑呢。"),
            ("拉兹", "我想这是他们在宇宙里移动的主要手段，毕竟他们身子太不轻巧了。"),
            ("方波", "不过虽然是全新的，也不知道它能支撑多久呢……"),
            ("拉兹", "我在终端界面下添加了能量条，方便随时检查剩余燃料。刚刚我们的试飞已经造成燃料的损耗了。"),
            ("方波", "起码这样不会出现开一半没油的情况……但也得寻找补充能量的方法。"),
            ("方波", "往前好像有出口，我们先飞出去看看吧。")
        );

        // ============ 关卡0：来到太空站外部平台 ============
        Create("Level0_AfterExit", false,
            ("拉兹", "看来。陨石破坏的这所空间站泄露了很多的能量……或许只要靠近它们，背包的能量就会被补充。"),
            ("拉兹", "又一个好消息，真不错，看来我们走了\"狗\"屎运？"),
            ("方波", "切……一点也不好笑。"),
            ("", "试着靠近前方的能量光点，并补充背包的能量吧！")
        );

        // ============ 关卡0：两个玩家成功补充能量 ============
        Create("Level0_AfterRecharge", false,
            ("方波", "能量条确实恢复了。"),
            ("拉兹", "好事一桩接一桩，你看前面那个黄色的——"),
            ("方波", "——一艘废弃的飞船？！而且刚好可以载人！"),
            ("拉兹", "周围环境扫描完毕。这附近的几个小行星把废弃空间站的一些零件纳入了它们的轨道，我们可以通过收集这些零件来修缮这座飞船。"),
            ("拉兹", "但首先，我们要先过去。"),
            ("", "尝试借助吸附、解除吸附和背包的帮助到达飞船吧！")
        );

        // ============ 关卡0：两个玩家成功到达飞船上 ============
        Create("Level0_AfterReachShip", false,
            ("方波", "这船应该是人类留下来的吧？我没见过这样的设计，真新鲜！"),
            ("拉兹", "我也没见过开关一个安在顶部一个安在底部的设计，或许我们俩需要同时站在标记的区域……")
        );

        // ============ 关卡1：进入 ============
        Create("Level1_Enter", false,
            ("方波", "这船状态不错，没有很严重的损坏……"),
            ("拉兹", "人类还是留下了不少有用的零件的。只要收集到这些东西，这艘飞船应该就还能修。清单同步给你了，你应该能看到，在终端的右上角。"),
            ("拉兹", "我们接下来要去行星中收集这些零件对吧？听着真刺激！你说等我们回去了，是不是也可以试着开发一个相关的主题乐园……就叫\"环球建成\"……"),
            ("拉兹", "好啦，等我们回去再琢磨。先开始收集零件吧。")
        );

        // ============ 关卡1：收集完所有零件后 ============
        Create("Level1_AfterCollect", false,
            ("方波", "这里的零件应该都收集完了？"),
            ("拉兹", "我看看……对，清单上显示已经完成了。"),
            ("拉兹", "继续加油，回到船上去吧，要去下一片小行星群了。"),
            ("", "每次收集结束后需要回到飞船上！")
        );

        // ============ 关卡2：进入 ============
        Create("Level2_Enter", false,
            ("方波", "刚刚我把新零件装到船上的时候，发现了这些引擎……"),
            ("拉兹", "很不错啊，利用它们的话，应该会让我们行动的更快一些。")
        );

        // ============ 关卡3：进入 ============
        Create("Level3_Enter", false,
            ("拉兹", "是我的错觉吗，我怎么感觉刚刚有个东西从那边飞过来了？"),
            ("方波", "哪里？我没看到——")
        );

        // ============ 关卡3：陨石砸在星球上的演出后 ============
        Create("Level3_AfterMeteor", false,
            ("方波", "什么情况？！"),
            ("拉兹", "现在我们都看到了……几十年过去了，附近星体的轨道还是不稳定。恐怕……我们又要遇到陨石雨了。"),
            ("拉兹", "起码前面这几颗我们还可以躲一躲……方波，一旦被砸到我们很可能会直接被弹飞，偏离这里小行星的重力。到时候就很难再回来收集东西了……"),
            ("方波", "被陨石撞飞了，应该再也不用考虑收集零件的事情了吧……"),
            ("拉兹", "小心为上，避着走吧。")
        );

        // ============ 关卡4：进入 ============
        Create("Level4_Enter", false,
            ("拉兹", "因为这些突然的陨石的原因……小行星的轨道也开始发生改变了，看准了再动。"),
            ("方波", "同时也要避开陨石对吧，我懂我懂。"),
            ("拉兹", "得心应爪了？真不错。")
        );

        // ============ 结局：成功 ============
        Create("Ending_Success", true,
            ("方波", "最后一个部分装上后……"),
            ("方波", "这样就大功告成了！"),
            ("拉兹", "引擎、推进器、仪表盘……都没问题，试着发动吧。趁着陨石还没把它再次砸坏……"),
            ("", "提示用投影亮起，黄色的飞船终于得以发动。两位探险家坐上驾驶舱，逃离身后的陨石雨。"),
            ("", "四面八方涌来的彗星与行星碎片撞击着人类文明在太阳系的最后一点残留，在附近的小行星身上留下大大小小的陨石坑。"),
            ("", "飞船的尾焰甩出反方向，这艘搭载着人类智慧的外星航船，会一路驶出银河系、一头栽进无名的星云，直到回到探险家温馨的母星。"),
            ("", "直到把人类文明的带回到人类都未曾探索过的地方。")
        );

        // ============ 结局：失败 ============
        Create("Ending_Failure", true,
            ("", "当你意识到自己不应该飞出这么远的时候，一切都晚了。"),
            ("", "飘在太空中确实让你想到很多事情：42、致远星的战况、还有应该提前看陨石雨的预报……"),
            ("", "无论如何，你们确实飘的越来越远了。\"方波和拉兹，我们有麻烦了。\"")
        );

        // ============ 清理旧资产（旧版对话结构已废弃） ============
        DeleteIfExists("Level1_AfterJetpack");   // 被 Level0 流程 + Level1_AfterCollect 替代
        DeleteIfExists("Level1_AfterAttach");    // 被 Level1_AfterCollect 替代
        DeleteIfExists("Level5_AfterMeteor");    // 已从新版脚本移除
    }

    // ==================== Helpers ====================

    static void Create(string name, bool needsCG, params (string speaker, string text)[] lines)
    {
        string path = $"{DIALOGUE_DIR}/{name}.asset";

        // 查找或创建
        DialogueSO so = AssetDatabase.LoadAssetAtPath<DialogueSO>(path);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<DialogueSO>();
            AssetDatabase.CreateAsset(so, path);
        }

        so.needsCG = needsCG;
        so.useTypingEffect = true;
        so.useAnimation = false;

        so.lines = new DialogueLine[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            so.lines[i] = new DialogueLine
            {
                speakerName = lines[i].speaker,
                text = lines[i].text,
                portrait = GetSmallPortrait(lines[i].speaker),
                bigPortrait = GetBigPortrait(lines[i].speaker),
            };
        }

        EditorUtility.SetDirty(so);
        Debug.Log($"  📄 {path} ({lines.Length} 行)");
    }

    /// <summary>删除废弃的旧对话资产</summary>
    static void DeleteIfExists(string name)
    {
        string path = $"{DIALOGUE_DIR}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<DialogueSO>(path);
        if (asset != null)
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"  🗑️ 已删除旧资产: {path}");
        }
    }

    /// <summary>从 CharacterConfig 缓存获取大立绘</summary>
    static Sprite GetBigPortrait(string speakerName)
    {
        if (string.IsNullOrEmpty(speakerName)) return null;

        string cfgPath = $"{CHAR_CONFIG_DIR}/Character_{speakerName}.asset";
        var cfg = AssetDatabase.LoadAssetAtPath<CharacterConfig>(cfgPath);
        return cfg != null ? cfg.bigPortrait : null;
    }

    /// <summary>从 CharacterConfig 缓存获取小头像</summary>
    static Sprite GetSmallPortrait(string speakerName)
    {
        if (string.IsNullOrEmpty(speakerName)) return null;

        string cfgPath = $"{CHAR_CONFIG_DIR}/Character_{speakerName}.asset";
        var cfg = AssetDatabase.LoadAssetAtPath<CharacterConfig>(cfgPath);
        return cfg != null ? cfg.smallPortrait : null;
    }

    static void EnsureDir(string dir)
    {
        // 逐级创建
        string[] parts = dir.TrimStart('/').Split('/');
        string current = "";
        foreach (var part in parts)
        {
            string prev = current;
            current = string.IsNullOrEmpty(prev) ? part : $"{prev}/{part}";
            if (!AssetDatabase.IsValidFolder(current))
            {
                string parent = string.IsNullOrEmpty(prev) ? "Assets" : prev;
                AssetDatabase.CreateFolder(parent, part);
            }
        }
    }
}
