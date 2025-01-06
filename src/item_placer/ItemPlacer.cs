namespace Shellguard;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Godot;

public interface IItemPlacer : INode2D
{
  void Enable();
  void Disable();
  void UpdateSelectDirection(Vector2 direction);
}

[Meta(typeof(IAutoNode))]
public partial class ItemPlacer : Node2D, IItemPlacer
{
  public void Enable() => Logic.Input(new ItemPlacerLogic.Input.Enable());

  public void Disable() => Logic.Input(new ItemPlacerLogic.Input.Disable());

  #region Exports
  [Export]
  private float _selectRange = 16;
  #endregion

  #region Nodes
  [Node]
  private Sprite2D Cursor { get; set; } = default!;
  #endregion

  #region Dependencies
  [Dependency]
  private IWorldRepo WorldRepo => this.DependOn<IWorldRepo>();
  #endregion

  #region State
  private ItemPlacerLogic Logic { get; set; } = default!;
  private ItemPlacerLogic.IBinding Binding { get; set; } = default!;
  #endregion

  #region Dependency Lifecycle
  public void Setup() => Logic = new();

  public void OnResolved()
  {
    Binding = Logic.Bind();

    // Bind functions to state outputs here
    Binding
      .Handle(
        (in ItemPlacerLogic.Output.UpdateCursorVisibility output) =>
          OnOutputUpdateCursorVisiblity(output.Visible)
      )
      .Handle(
        (in ItemPlacerLogic.Output.UpdatePosition output) => OnOutputUpdatePosition(output.Position)
      );

    Logic.Set(WorldRepo);
    Logic.Start();
  }
  #endregion

  #region Godot Lifecycle
  public override void _Notification(int what) => this.Notify(what);

  public void OnReady() { }

  public void OnExitTree()
  {
    Logic.Stop();
    Binding.Dispose();
  }
  #endregion

  #region Input Callbacks
  public void UpdateSelectDirection(Vector2 direction)
  {
    var pos = SnapGlobalPositionToGrid(GlobalPosition + (direction * _selectRange));
    Logic.Input(new ItemPlacerLogic.Input.TryUpdatePosition(pos));
  }
  #endregion

  #region Output Callbacks
  private void OnOutputUpdatePosition(Vector2I position) =>
    Cursor.GlobalPosition = SnapGlobalPositionToGrid(position);

  private void OnOutputUpdateCursorVisiblity(bool visible) => Visible = visible;
  #endregion


  public Vector2I SnapGlobalPositionToGrid(Vector2 globalPosition) =>
    ((Vector2I)globalPosition - (Vector2I.One * 8)).Snapped(16);

  public override void _UnhandledInput(InputEvent @event)
  {
    if (Input.IsKeyLabelPressed(Key.Space))
    {
      Logic.Input(
        new ItemPlacerLogic.Input.TryPlantTree((Vector2I)Cursor.GlobalPosition + Vector2I.One * 8)
      );
    }
  }
}

public interface ITileSelectorLogic : ILogicBlock<ItemPlacerLogic.State>;

[Meta]
[LogicBlock(typeof(State), Diagram = true)]
public partial class ItemPlacerLogic : LogicBlock<ItemPlacerLogic.State>, ITileSelectorLogic
{
  public override Transition GetInitialState() => To<State.Enabled>();

  public static class Input
  {
    public record struct Enable;

    public record struct Disable;

    public record struct TryUpdatePosition(Vector2I Position);

    public record struct TryPlantTree(Vector2I Tile);
  }

  public static class Output
  {
    public record struct UpdatePosition(Vector2I Position);

    public record struct UpdateCursorVisibility(bool Visible);
  }

  public abstract partial record State : StateLogic<State>
  {
    public State()
    {
      OnAttach(() => { });
      OnDetach(() => { });
    }

    public partial record Enabled
      : State,
        IGet<Input.Disable>,
        IGet<Input.TryPlantTree>,
        IGet<Input.TryUpdatePosition>
    {
      public Enabled()
      {
        this.OnEnter(() => Output(new Output.UpdateCursorVisibility(true)));
      }

      public Transition On(in Input.Disable input) => To<Disabled>();

      public Transition On(in Input.TryPlantTree input)
      {
        var worldRepo = Get<IWorldRepo>();

        if (worldRepo.IsTileFree(input.Tile))
        {
          worldRepo.PlantTree(input.Tile);
        }

        return ToSelf();
      }

      public Transition On(in Input.TryUpdatePosition input)
      {
        Output(new Output.UpdatePosition(input.Position));
        return ToSelf();
      }
    }

    public partial record Disabled : State, IGet<Input.Enable>
    {
      public Disabled()
      {
        this.OnEnter(() => Output(new Output.UpdateCursorVisibility(false)));
      }

      public Transition On(in Input.Enable input) => To<Enabled>();
    }
  }
}