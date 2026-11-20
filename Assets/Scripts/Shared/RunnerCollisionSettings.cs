using UnityEngine;

namespace KaplaoGames.Shared
{
  public static class RunnerCollisionSettings
  {
    public const string RunnerLayerName = "Runner";

    static int runnerLayer = -1;

    public static int RunnerLayer
    {
      get
      {
        if (runnerLayer < 0)
          runnerLayer = LayerMask.NameToLayer(RunnerLayerName);
        return runnerLayer;
      }
    }

    public static void SetRunnerLayer(GameObject root)
    {
      int layer = RunnerLayer;
      if (layer < 0)
        return;

      SetLayerRecursive(root.transform, layer);
    }

    public static void ApplyRunnersCanInteract(bool canInteract)
    {
      int layer = RunnerLayer;
      if (layer < 0)
        return;

      Physics2D.IgnoreLayerCollision(layer, layer, !canInteract);
    }

    static void SetLayerRecursive(Transform root, int layer)
    {
      root.gameObject.layer = layer;
      for (int i = 0; i < root.childCount; i++)
        SetLayerRecursive(root.GetChild(i), layer);
    }
  }
}
