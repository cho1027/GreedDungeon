using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>휴식·보물방 전용 화면. 보상 상태는 RoomUsageData에만 저장하고 UI는 그 상태를 표시합니다.</summary>
public class SpecialRoomUI : MonoBehaviour
{
    public bool Closed { get; private set; }
    private SpecialRoomTrigger config;
    private RoomContext context;
    private RoomUsageData state;
    private RectTransform panel;
    private string notice;

    public void Initialize(SpecialRoomTrigger settings, RoomContext ctx, RoomUsageData data)
    {
        config = settings; context = ctx; state = data;
        RoomRunData.RunReset += CloseForNewRun;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        panel = Element("Black background", transform, Vector2.zero, new Vector2(1920,1080));
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = Color.black;
        Render();
    }

    private void CloseForNewRun()
    {
        Closed = true;
        Destroy(gameObject);
    }

    private void OnDestroy() => RoomRunData.RunReset -= CloseForNewRun;

    private void Render()
    {
        // 이전 버튼은 즉시 비활성화하여 같은 프레임의 중복 클릭을 막습니다.
        foreach (Transform child in panel) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Text(config.treasure ? "보물방" : "휴식방", new Vector2(0,450), new Vector2(1300,80), 42);
        Texture2D art = config.treasure ? (state.activated ? config.chestOpen : config.chestClosed)
            : (state.activated ? config.campfireLit : config.campfireUnlit);
        bool unlitPlaceholder = !config.treasure && !state.activated && art == null;
        if (unlitPlaceholder) art = config.campfireLit;
        if (art != null)
        {
            var image = Element("Room art", panel, new Vector2(0,140), new Vector2(900,506));
            var raw = image.gameObject.AddComponent<RawImage>(); raw.texture = art;
            raw.color = unlitPlaceholder ? new Color(0.16f,0.16f,0.16f,1) : Color.white;
            raw.raycastTarget = false;
            // 상태별 원본 해상도가 조금 달라도 같은 영역에 표시하여 전환 시 크기 변화를 막습니다.
            Texture2D layoutArt = config.treasure ? (config.chestOpen != null ? config.chestOpen : art)
                : (config.campfireLit != null ? config.campfireLit : art);
            float aspect = (float)layoutArt.width / layoutArt.height;
            image.sizeDelta = aspect >= 900f/506f ? new Vector2(900,900/aspect) : new Vector2(506*aspect,506);
        }
        else Text(state.activated ? "열린 상자 (임시 표시)" : "닫힌 상자 (임시 표시)",
            new Vector2(0,150),new Vector2(1000,180),48);

        string status = notice ?? state.result;
        if (string.IsNullOrEmpty(status)) status = state.completed ? "이미 이용한 방입니다." :
            state.activated ? (config.treasure && state.offers.Count == 0 ? "상자를 열었습니다. 유물은 추후 추가됩니다." : "보상 하나를 선택하세요.") : config.treasure ? "상자를 열어 보상을 확인하세요." : "모닥불에 불을 붙이세요.";
        if (state.completed) status = "이미 이용한 방입니다. " + state.result;
        Text(status,new Vector2(0,-150),new Vector2(1600,100),28);
        if (!state.completed)
        {
            if (!state.activated) Button(config.treasure ? "상자 열기" : "불 붙이기",new Vector2(0,-270),new Vector2(500,80),() =>
            {
                state.activated = true;
                if (config.treasure && config.relicPool != null && config.relicPool.Length > 0)
                    TreasureRoomController.Prepare(state,config.relicPool,RoomRunData.Relics,config.developmentMode);
                Render();
            });
            else if (!config.treasure)
            {
                Button("최대 체력의 30% 회복",new Vector2(-310,-270),new Vector2(560,90),() =>
                {
                    if (!RestRoomController.Recover(state,context.Player.GetComponent<PlayerHealth>())) notice="살아 있는 플레이어의 PlayerHealth가 필요합니다.";
                    Render();
                });
                Button("강화",new Vector2(310,-270),new Vector2(560,90),() =>
                {
                    var upgrade = context.Player.GetComponent<IRestRoomUpgrade>();
                    RestRoomController.Upgrade(state,context.Player,upgrade); Render();
                });
            }
            else
            {
                RenderOffers();
            }
        }
        if (state.completed && config.treasure) RenderOffers();
        Button(state.completed ? "던전으로 돌아가기" : "나가기 (선택 상태 유지)",new Vector2(0,-470),new Vector2(650,70),()=> Closed=true);
    }

    // 선택 후에도 세 슬롯을 유지하여 획득한 항목과 포기한 항목을 구분합니다.
    private void RenderOffers()
    {
        if (state.offers.Count != 3)
        {
            string message = config.relicPool == null || config.relicPool.Length == 0
                ? "아직 준비된 유물이 없습니다. 보상은 지급되지 않습니다."
                : "선택 가능한 유물 데이터 3개가 필요합니다.";
            Text(message,new Vector2(0,-280),new Vector2(1300,100),26);
            return;
        }
        for (int i=0; i<3; i++)
        {
            int index=i;
            RelicData relic=state.offers[i];
            string label=relic != null ? $"{relic.relicName}\n{relic.description}" : "유물 데이터 없음";
            bool selected = relic != null && relic.relicId == state.selectedRelicId;
            if (relic != null && relic.developmentOnly) label="[개발용·효과 없음]\n"+label;
            if (state.completed) label=(selected ? "[획득]\n" : "[선택하지 않음]\n")+label;
            var button=Button(label,new Vector2((i-1)*560,-300),new Vector2(520,210),() =>
            { TreasureRoomController.Choose(state,index,RoomRunData.Relics); Render(); });
            button.interactable = !state.completed && RoomRunData.Relics.CanAcquire(relic) &&
                (!relic.developmentOnly || config.developmentMode);
            if(state.completed) button.GetComponent<Image>().color = selected ? new Color(.45f,.28f,.08f) : new Color(.08f,.08f,.08f);
            if(relic != null && relic.icon != null)
            {
                var icon=Element("Relic icon",button.transform,new Vector2(-200,55),new Vector2(60,60));
                var image=icon.gameObject.AddComponent<Image>();image.sprite=relic.icon;image.preserveAspect=true;image.raycastTarget=false;
            }
        }
    }

    private RectTransform Element(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform)); var rect=go.GetComponent<RectTransform>();
        rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=pos;return rect;
    }
    private void Text(string value,Vector2 pos,Vector2 size,int fontSize,Transform parent=null)
    {
        var label=Element("Label",parent ?? panel,pos,size).gameObject.AddComponent<TextMeshProUGUI>();
        label.text=value;label.font=config.font != null ? config.font : TMP_Settings.defaultFontAsset;
        label.fontSize=fontSize;label.alignment=TextAlignmentOptions.Center;label.color=new Color(1,.9f,.73f);label.raycastTarget=false;
    }
    private Button Button(string label,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
    {
        var rect=Element(label,panel,pos,size);rect.gameObject.AddComponent<Image>().color=new Color(.2f,.13f,.08f,1);
        var button=rect.gameObject.AddComponent<Button>();button.onClick.AddListener(action);
        Text(label,Vector2.zero,size-new Vector2(35,15),25,rect);return button;
    }
}
