using System.Collections;
using Telemetry.ExperimentFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUploadOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject overlayRoot;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text warningText;

    [SerializeField] private Slider progressBar;

    [Header("Settings")]
    [SerializeField] private float successVisibleSeconds = 2f;

    private EndFlowController endFlowController;
    private Coroutine successRoutine;

    private void OnEnable()
    {
        ResolveController();

        if (endFlowController != null)
            endFlowController.OnUploadStateChanged += HandleUploadStateChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (endFlowController != null)
            endFlowController.OnUploadStateChanged -= HandleUploadStateChanged;
    }

    private void ResolveController()
    {
        if (endFlowController == null)
            endFlowController = FindObjectOfType<EndFlowController>();
    }

    private void HandleUploadStateChanged(EndUploadState state)
    {
        Refresh();
    }

    private void Refresh()
    {
        ResolveController();

        if (endFlowController == null)
        {
            overlayRoot.SetActive(false);
            return;
        }

        switch (endFlowController.UploadState)
        {
            case EndUploadState.None:
                overlayRoot.SetActive(false);
                break;

            case EndUploadState.Uploading:
                ShowUploading();
                break;

            case EndUploadState.Succeeded:
                ShowSuccess();
                break;

            case EndUploadState.Failed:
                ShowFailure();
                break;
        }
    }

    private void ShowUploading()
    {
        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();

        titleText.text = "WYSY£ANIE DANYCH";

        bodyText.text =
            "Trwa zapisywanie i wysy³anie danych badania.";

        warningText.text =
            "NIE ZDEJMUJ JESZCZE GOGLI";

        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(true);

            if (successRoutine != null)
            {
                StopCoroutine(successRoutine);
                successRoutine = null;
            }

            successRoutine = StartCoroutine(AnimateProgressBar());
        }
    }

    private void ShowSuccess()
    {
        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();

        StopProgressAnimation();

        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(true);
            progressBar.value = 1f;
        }

        titleText.text = "DANE ZOSTA£Y WYS£ANE";

        bodyText.text =
            "Sesja zosta³a bezpiecznie przes³ana.";

        warningText.text =
            "MO¯ESZ TERAZ ZDJ¥Æ GOGLE";

        if (successRoutine != null)
            StopCoroutine(successRoutine);

        successRoutine = StartCoroutine(HideSuccessRoutine());
    }

    private void ShowFailure()
    {
        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();

        StopProgressAnimation();

        if (progressBar != null)
            progressBar.gameObject.SetActive(false);

        titleText.text = "B£¥D WYSY£ANIA";

        bodyText.text =
            "Dane zosta³y zapisane lokalnie, ale nie uda³o siê ich wys³aæ.";

        warningText.text =
            "NIE WY£¥CZAJ GOGLI. ZG£OŒ PROBLEM OPERATOROWI.";
    }

    private IEnumerator AnimateProgressBar()
    {
        if (progressBar == null)
            yield break;

        while (endFlowController != null &&
               endFlowController.UploadState == EndUploadState.Uploading)
        {
            progressBar.value += Time.deltaTime * 0.35f;

            if (progressBar.value >= 1f)
                progressBar.value = 0f;

            yield return null;
        }
    }

    private void StopProgressAnimation()
    {
        if (successRoutine != null)
        {
            StopCoroutine(successRoutine);
            successRoutine = null;
        }
    }

    private IEnumerator HideSuccessRoutine()
    {
        yield return new WaitForSeconds(successVisibleSeconds);

        if (endFlowController != null)
            endFlowController.ClearCompletedUploadState();

        overlayRoot.SetActive(false);
        successRoutine = null;
    }
}