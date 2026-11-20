using UnityEngine;

namespace KaplaoGames.Shared
{
  [CreateAssetMenu(fileName = "RunnerCharacterLibrary", menuName = "Kaplao/Runner Character Library")]
  public class RunnerCharacterLibrary : ScriptableObject
  {
    const string DefaultResourcePath = "RunnerCharacterLibrary";

    [SerializeField] AnimatorOverrideController[] characters = System.Array.Empty<AnimatorOverrideController>();
    [SerializeField] int publicCharacterCount = RunnerSkinCatalog.PublicSkinCount;

    public int CharacterCount => characters != null ? characters.Length : 0;
    public int PublicCharacterCount => Mathf.Clamp(publicCharacterCount, 0, CharacterCount);

    public static RunnerCharacterLibrary LoadDefault()
    {
      return Resources.Load<RunnerCharacterLibrary>(DefaultResourcePath);
    }

    public RuntimeAnimatorController GetController(int index)
    {
      if (characters == null || characters.Length == 0)
        return null;

      if (index < 0 || index >= characters.Length)
        index = 0;

      return characters[index];
    }

    public int PickRandomIndex()
    {
      return PickRandomPublicIndex();
    }

    public int PickRandomPublicIndex()
    {
      int count = PublicCharacterCount;
      if (count <= 0)
        return 0;

      return Random.Range(0, count);
    }

    public bool IsAdminSkinIndex(int index) => index >= PublicCharacterCount;
  }
}
