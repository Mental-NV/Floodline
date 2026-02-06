using System.Collections.Generic;
using UnityEngine;
using Floodline.Core;

namespace Floodline.Client
{
    /// <summary>
    /// Displays the list of level objectives with progress bars and completion status.
    /// Read-only: sourced directly from simulation.Objectives.
    /// </summary>
    public class ObjectivesDisplay : MonoBehaviour
    {
        [SerializeField]
        private Transform objectiveContainer;

        [SerializeField]
        private GameObject objectivePrefab;

        private readonly List<ObjectiveDisplayItem> displayItems = new();

        public void UpdateObjectives(ObjectiveSet objectives)
        {
            if (objectives == null || objectives.Objectives.Count == 0)
            {
                // No objectives; hide container
                if (objectiveContainer != null)
                    objectiveContainer.gameObject.SetActive(false);
                return;
            }

            // Ensure we have enough display items
            while (displayItems.Count < objectives.Objectives.Count)
            {
                if (objectivePrefab == null || objectiveContainer == null)
                    break;

                GameObject instance = Instantiate(objectivePrefab, objectiveContainer);
                ObjectiveDisplayItem item = instance.GetComponent<ObjectiveDisplayItem>();
                if (item != null)
                    displayItems.Add(item);
            }

            // Update each objective
            for (int i = 0; i < objectives.Objectives.Count && i < displayItems.Count; i++)
            {
                ObjectiveProgress progress = objectives.Objectives[i];
                displayItems[i].UpdateProgress(progress);
                displayItems[i].gameObject.SetActive(true);
            }

            // Hide unused items
            for (int i = objectives.Objectives.Count; i < displayItems.Count; i++)
            {
                displayItems[i].gameObject.SetActive(false);
            }

            if (objectiveContainer != null)
                objectiveContainer.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Individual objective progress item (prefab instance).
    /// </summary>
    public class ObjectiveDisplayItem : MonoBehaviour
    {
        [SerializeField]
        private TMPro.TextMeshProUGUI objectiveTypeText;

        [SerializeField]
        private TMPro.TextMeshProUGUI progressText;

        [SerializeField]
        private UnityEngine.UI.Image progressBar;

        public void UpdateProgress(ObjectiveProgress progress)
        {
            if (progress == null)
                return;

            float ratio = progress.Target > 0 ? (float)progress.Current / progress.Target : 0f;

            if (objectiveTypeText != null)
                objectiveTypeText.text = progress.Type.ToString();

            if (progressText != null)
                progressText.text = $"{progress.Current}/{progress.Target}";

            if (progressBar != null)
                progressBar.fillAmount = Mathf.Clamp01(ratio);
        }
    }
}
