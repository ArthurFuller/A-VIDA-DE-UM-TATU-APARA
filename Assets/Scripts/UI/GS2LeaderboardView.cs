using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class GS2LeaderboardView : MonoBehaviour
{
    [SerializeField] private Text status;
    [SerializeField] private RectTransform rowTemplate;
    [SerializeField, Min(0)] private float rowGap = 8f;
    [SerializeField, Min(1)] private float visibleHeight = 165f;

    private RectTransform content;
    private ScrollRect scroll;

    private void Awake()
    {
        if (rowTemplate == null) return;
        var viewport = new GameObject("Recorte dos resultados", typeof(RectTransform),
            typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(transform, false);
        float rowHeight = rowTemplate.rect.height;
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = rowTemplate.anchoredPosition -
            Vector2.up * ((visibleHeight - rowHeight) * 0.5f);
        viewport.sizeDelta = new Vector2(rowTemplate.rect.width, visibleHeight);
        viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        content = new GameObject("Resultados", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;

        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        rowTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (rowTemplate == null) return;
        if (status != null) status.text = "Carregando ranking...";
        GS2LeaderboardOnline.Instance.Load(ShowEntries);
    }

    private void ShowEntries(List<GS2LeaderboardOnline.Entry> entries, string error)
    {
        if (!isActiveAndEnabled || content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        if (entries == null)
        {
            if (status != null) status.text = error;
            if (GS2LeaderboardScore.HasLocalScore)
                AddRow("—", GS2Profile.HasNickname ? GS2Profile.Nickname : "Jogador",
                    GS2LeaderboardScore.LocalBestCoins.ToString(), 0);
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
                AddRow((i + 1).ToString(), entries[i].nickname, entries[i].coins.ToString(), i);
            if (status != null) status.text = entries.Count == 0 ? "Nenhuma partida no ranking." : string.Empty;
        }
        int count = entries == null ? (GS2LeaderboardScore.HasLocalScore ? 1 : 0) : entries.Count;
        float rowHeight = rowTemplate.rect.height;
        content.sizeDelta = new Vector2(0, Mathf.Max(visibleHeight,
            count * rowHeight + Mathf.Max(0, count - 1) * rowGap));
        scroll.verticalNormalizedPosition = 1f;
    }

    private void AddRow(string position, string nickname, string coins, int index)
    {
        RectTransform row = Instantiate(rowTemplate, content);
        row.gameObject.SetActive(true);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 1);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0, -row.rect.height * 0.5f -
            index * (row.rect.height + rowGap));
        row.GetChild(0).GetComponent<Text>().text = position;
        row.GetChild(1).GetComponent<Text>().text = nickname;
        row.GetChild(2).GetComponent<Text>().text = coins;
    }
}
