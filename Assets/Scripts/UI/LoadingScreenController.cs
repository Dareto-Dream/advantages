using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreenController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI headline;

    [Tooltip("8 rows, in MatchManager spawn order: index 0 is you, 1..TeamSize-1 are allies, the rest are the enemy team.")]
    [SerializeField] private List<GameObject> rowRoots = new List<GameObject>();
    [SerializeField] private List<TextMeshProUGUI> nameLabels = new List<TextMeshProUGUI>();
    [SerializeField] private List<Image> fillBars = new List<Image>();
    [SerializeField] private List<TextMeshProUGUI> statusLabels = new List<TextMeshProUGUI>();

    private bool loading;

    public void BeginLoad(string sceneName)
    {
        if (loading)
        {
            return;
        }

        loading = true;
        DontDestroyOnLoad(gameObject);
        gameObject.SetActive(true);
        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        int slots = Mathf.Clamp(MatchSettings.TeamSize * 2, 1, nameLabels.Count);
        float[] fake = new float[nameLabels.Count];
        float[] fakeDuration = new float[nameLabels.Count];
        System.Random rng = new System.Random();

        for (int i = 0; i < rowRoots.Count; i++)
        {
            bool active = i < slots;
            if (rowRoots[i] != null)
            {
                rowRoots[i].SetActive(active);
            }

            if (!active)
            {
                continue;
            }

            bool you = i == 0;
            string label = you
                ? $"{MatchSettings.PlayerName}  <size=70%><color=#33D9B8>YOU</color></size>"
                : i < MatchSettings.TeamSize ? $"Ally {i}" : $"Enemy {i - MatchSettings.TeamSize + 1}";

            if (i < nameLabels.Count && nameLabels[i] != null)
            {
                nameLabels[i].text = label;
            }

            if (i < fillBars.Count && fillBars[i] != null)
            {
                fillBars[i].fillAmount = 0f;
            }

            if (i < statusLabels.Count && statusLabels[i] != null)
            {
                statusLabels[i].text = "LOADING";
            }

            fakeDuration[i] = you ? 0f : (float)(0.6 + rng.NextDouble() * 1.4);
        }

        if (headline != null)
        {
            headline.text = "LOADING MATCH";
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            bool allReady = true;

            for (int i = 0; i < slots; i++)
            {
                fake[i] = i == 0
                    ? Mathf.Clamp01(operation.progress / 0.9f)
                    : (fakeDuration[i] <= 0f ? 1f : Mathf.Clamp01(elapsed / fakeDuration[i]));

                if (i < fillBars.Count && fillBars[i] != null)
                {
                    fillBars[i].fillAmount = fake[i];
                }

                if (i < statusLabels.Count && statusLabels[i] != null)
                {
                    statusLabels[i].text = fake[i] >= 1f ? "READY" : "LOADING";
                }

                if (fake[i] < 1f)
                {
                    allReady = false;
                }
            }

            if (allReady)
            {
                break;
            }

            yield return null;
        }

        if (headline != null)
        {
            headline.text = "ENTERING MATCH";
        }

        yield return new WaitForSecondsRealtime(0.3f);

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }

        Destroy(gameObject);
    }
}
