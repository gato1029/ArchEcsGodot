using Arch.AOT.SourceGenerator;
using Arch.Buffer;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;

using Godot;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;


[Component]
public struct HumanController
{

}
[Component]
public struct IAController
{

}
[Component]
public struct ThirdPersonController
{

}
internal class UnitControllerSystem : BaseSystem<World, float>
{
    private CommandBuffer commandBuffer;
    private QueryDescription queryHuman = new QueryDescription().WithAll<Unit,Position,HumanController,Collider>();    
    public UnitControllerSystem(World world) : base(world)
    {
        commandBuffer = new CommandBuffer();
    }

    private readonly struct ProcessJobHuman : IForEachWithEntity<Position,Velocity,Direction,Rotation,Collider>
    {
        private readonly float _deltaTime;
        private readonly CommandBuffer _commandBuffer;
        private readonly InputHandler _inputHandler;

        public ProcessJobHuman(float deltaTime, CommandBuffer commandBuffer, InputHandler inputHandler)
        {
            _deltaTime = deltaTime;
            _commandBuffer = commandBuffer;
            _inputHandler = inputHandler;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(Entity entity, ref Position p,ref Velocity v, ref Direction d, ref Rotation r, ref Collider c)
        {
            Vector2 moveDirection = Vector2.Zero;

            if (_inputHandler.IsActionActive("move_up"))
                moveDirection.Y -= 1;
            if (_inputHandler.IsActionActive("move_down"))
                moveDirection.Y += 1;
            if (_inputHandler.IsActionActive("move_left"))
                moveDirection.X -= 1;
            if (_inputHandler.IsActionActive("move_right"))
                moveDirection.X += 1;
            if (moveDirection != Vector2.Zero)
            {                              
                moveDirection = moveDirection.Normalized();                
                d.value = moveDirection;
                
                if (!entity.Has<PendingTransform>() && r.value != Mathf.RadToDeg(d.value.Angle()))
                {
                    r.value = Mathf.RadToDeg(d.value.Angle());
                    _commandBuffer.Add<PendingTransform>(entity);
                }
                
                Vector2 movement = d.value * v.value * _deltaTime;
                Vector2 movementNext = p.value + movement;
                GD.Print(movement);
                var resultList = CollisionManager.dynamicCollidersEntities.GetPossibleQuadrants(movementNext, 128);
                bool existCollision = false;
                foreach (var itemMap in resultList)
                {
                    foreach (var item in itemMap.Value)
                    {
                        if (item.Key != entity.Id)
                        {
                            Entity entB = item.Value;

                            var entityExternal = entB.Get<Collider>().rectTransform;
                            var entityExternalPos = entB.Get<Position>().value;

                            if (CollisionManager.CheckAABBCollision(movementNext, c.rectTransform, entityExternalPos, entityExternal))
                            {
                                existCollision = true;
                                break;
                            }
                        }
                    }

                }
                if (!existCollision)
                {                  
                    p.value += movement;                                   
                }


            }
            if (_inputHandler.IsActionActive("attack"))
            {
                if (!entity.Has<OrderAtack>())
                {
                    _commandBuffer.Add<OrderAtack>(entity);
                }                
            }
        }
    }

    public override void Update(in float t)
    {
        //uint idDocker = ServiceLocator.Instance.GetService<NodoPrincipal>().idDocker;
        //ImGui.SetNextWindowDockID(idDocker, ImGuiCond.Once);
        //ImGui.SetNextWindowPos(new System.Numerics.Vector2(200, 150),ImGuiCond.FirstUseEver);        
        //ImGui.Begin("Flat Window at Specific Position");
        
        //ImGui.End();

        var job = new ProcessJobHuman(TimeGodot.Delta, commandBuffer, ServiceLocator.Instance.GetService<InputHandler>());
        World.InlineEntityQuery<ProcessJobHuman, Position,Velocity,Direction,Rotation,Collider>(in queryHuman, ref job);
        commandBuffer.Playback(World);
        

    }
}

