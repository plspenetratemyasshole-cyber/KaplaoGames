using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
  [DisallowMultipleComponent]
  public class RunnerAppearanceClient : NetworkBehaviour
  {
    [SyncVar(hook = nameof(OnCharacterIndexChanged))]
    int characterIndex = -1;

    public int CharacterIndex => characterIndex;

    [SerializeField] Animator animator;

    void Awake()
    {
      if (animator == null)
      {
        Transform visual = transform.Find("Visual");
        if (visual != null)
          animator = visual.GetComponent<Animator>();
      }
    }

    public override void OnStartServer()
    {
      if (characterIndex >= 0)
        return;

      if (!TryGetComponent(out NetworkPlayer networkPlayer))
        return;

      ServerAssignCharacter(
        networkPlayer.preferredSkinIndex,
        networkPlayer.serverOwnedSkinIds,
        networkPlayer.serverSkinOwnershipVerified);
    }

    [Server]
    public void ServerAssignRandomCharacter()
    {
      ServerAssignCharacter(RunnerSkinPreference.RandomIndex, null, false);
    }

    [Server]
    public void ServerAssignCharacter(int preferredIndex, string[] ownedSkinIds, bool ownershipVerified)
    {
      RunnerCharacterLibrary library = RunnerCharacterLibrary.LoadDefault();
      if (library == null || library.CharacterCount == 0)
      {
        characterIndex = 0;
        return;
      }

      characterIndex = RunnerSkinCatalog.ResolveServerSkinIndex(
        preferredIndex,
        ownedSkinIds,
        ownershipVerified);
      characterIndex = Mathf.Clamp(characterIndex, 0, library.CharacterCount - 1);
    }

    public override void OnStartClient()
    {
      ApplyCharacter(characterIndex);
    }

    void OnCharacterIndexChanged(int oldIndex, int newIndex)
    {
      ApplyCharacter(newIndex);
    }

    void ApplyCharacter(int index)
    {
      if (animator == null)
        return;

      RunnerCharacterLibrary library = RunnerCharacterLibrary.LoadDefault();
      if (library == null)
        return;

      RuntimeAnimatorController controller = library.GetController(index);
      if (controller != null)
        animator.runtimeAnimatorController = controller;
    }

#if UNITY_EDITOR
    protected new void OnValidate()
    {
      if (animator == null)
      {
        Transform visual = transform.Find("Visual");
        if (visual != null)
          animator = visual.GetComponent<Animator>();
      }
    }
#endif
  }
}
