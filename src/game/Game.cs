namespace Shellguard.Game;

using System.Collections.Generic;
using System.Linq;
using Chickensoft.AutoInject;
using Chickensoft.Collections;
using Chickensoft.GoDotLog;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.SaveFileBuilder;
using Godot;
using Shellguard.Game.Domain;
using Shellguard.Game.State;
using Shellguard.Player;
using Shellguard.Save;
using Shellguard.Tree;

public interface IGame
  : INode2D,
    IProvide<IGameRepo>,
    IProvide<ISaveChunk<GameData>>,
    IProvide<Dictionary<string, TreeData>>
{
  void StartNewGame();
  void RequestLoadGame();
  void RequestSaveGame();
}

[Meta(typeof(IAutoNode))]
public partial class Game : Node2D, IGame
{
  private readonly GDLog _log = new(nameof(Game));

  #region Save
  private ISaveChunk<GameData> GameChunk { get; set; } = default!;
  private Dictionary<string, TreeData> TreeDictionary { get; set; } = [];

  #endregion

  #region Nodes
  [Node]
  private Node2D Trees { get; set; } = default!;
  #endregion

  #region State
  public IGameRepo GameRepo { get; set; } = default!;
  public IGameLogic Logic { get; set; } = default!;
  public GameLogic.IBinding Binding { get; set; } = default!;
  #endregion

  #region Provisions
  ISaveChunk<GameData> IProvide<ISaveChunk<GameData>>.Value() => GameChunk;

  Dictionary<string, TreeData> IProvide<Dictionary<string, TreeData>>.Value() => TreeDictionary;

  public IGameRepo Value() => GameRepo;
  #endregion

  #region Dependencies
  [Dependency]
  private IGameFileService GameFileService => this.DependOn<IGameFileService>();

  [Dependency]
  private IAppRepo AppRepo => this.DependOn<IAppRepo>();
  #endregion

  #region Dependency Lifecycle
  public void Setup()
  {
    GameChunk = new SaveChunk<GameData>(
      onSave: (chunk) =>
      {
        var gameData = new GameData()
        {
          PlayerData = chunk.GetChunkSaveData<PlayerData>(),
          TreeDictionary = TreeDictionary,
        };

        foreach (var entry in TreeDictionary)
        {
          GD.Print(entry.Key);
        }

        return gameData;
      },
      onLoad: (chunk, data) =>
      {
        chunk.LoadChunkSaveData(data.PlayerData);
        TreeDictionary = data.TreeDictionary;
      }
    );

    Logic = new GameLogic();
  }

  public void OnResolved()
  {
    GameFileService.Chunk = GameChunk;
    GameFileService.SelectGameFile(0); // TODO later, do this in a more customizable way.

    GameRepo = new GameRepo(GameFileService);
    Logic.Set(GameRepo);
    Logic.Set(AppRepo);

    Binding = Logic.Bind();
    Binding.Handle((in GameLogic.Output.SetPauseMode output) => SetGamePaused(output.IsPaused));

    this.Provide();
  }
  #endregion


  #region Input Callbacks
  public void StartNewGame() => Logic.Input(new GameLogic.Input.StartGame());

  public void RequestLoadGame() => Logic.Input(new GameLogic.Input.RequestLoad());

  public void RequestSaveGame() => Logic.Input(new GameLogic.Input.RequestSave());
  #endregion

  #region Output Callbacks
  private void SetGamePaused(bool isPaused) => GetTree().Paused = isPaused;
  #endregion

  #region Godot Lifecycle
  public override void _Notification(int what) => this.Notify(what);

  public override void _Input(InputEvent @event)
  {
    if (Input.IsActionJustPressed(Inputs.Esc))
    {
      Logic.Input(new GameLogic.Input.PauseButtonPressed());
    }
    else if (Input.IsActionJustPressed(Inputs.Quicksave))
    {
      RequestSaveGame();
    }
    else if (Input.IsActionJustPressed(Inputs.Quickload))
    {
      RequestLoadGame();
    }
  }

  public void OnExitTree()
  {
    Logic.Stop();
    Binding.Dispose();
    GameRepo.Dispose();
  }
  #endregion
}
