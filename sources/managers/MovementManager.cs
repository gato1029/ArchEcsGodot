using Arch.AOT.SourceGenerator;
using Arch.Buffer;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Godot.HttpRequest;


public enum MovementType
{
    CIRCLE,SQUARE
}
[Component]
public struct AreaMovement
{
    public uint value;
    public uint value2;
    public MovementType type;
}
[Component]
public struct TargetMovement
{
    public Vector2 value;    
}
[Component]
public struct Direction
{
    public Vector2 value;
}
[Component]
public struct Velocity
{
    public float value;
}
internal class MovementManager: BaseSystem<World, float>
{
    private CommandBuffer commandBuffer;
    private QueryDescription query = new QueryDescription().WithAll<Position,Velocity, TargetMovement,Direction,Collider, Rotation>();    
    public MovementManager(World world) : base(world)
    {
        commandBuffer = new CommandBuffer();
    }

    private struct ChunkJobMovement : IChunkJob
    {
        private readonly float _deltaTime;
        private readonly CommandBuffer _commandBuffer;

        public ChunkJobMovement(CommandBuffer commandBuffer, float deltaTime) : this()
        {
            _commandBuffer = commandBuffer;
            _deltaTime = deltaTime;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Execute(ref Chunk chunk)
        {
            ref var pointerEntity = ref chunk.Entity(0);
            ref var pointerPosition = ref chunk.GetFirst<Position>();
            ref var pointerVelocity = ref chunk.GetFirst<Velocity>();
            ref var pointerTargetMovement = ref chunk.GetFirst<TargetMovement>();
            ref var pointerDirection = ref chunk.GetFirst<Direction>();
            ref var pointerCollider = ref chunk.GetFirst<Collider>();
            ref var pointerRotation = ref chunk.GetFirst<Rotation>();
           
            foreach (var entityIndex in chunk)
            {
                ref Entity entity = ref Unsafe.Add(ref pointerEntity, entityIndex);
                ref Position p = ref Unsafe.Add(ref pointerPosition, entityIndex);
                ref Velocity v = ref Unsafe.Add(ref pointerVelocity, entityIndex);
                ref TargetMovement tm = ref Unsafe.Add(ref pointerTargetMovement, entityIndex);
                ref Direction d = ref Unsafe.Add(ref pointerDirection, entityIndex);
                ref Collider c = ref Unsafe.Add(ref pointerCollider, entityIndex);
                ref Rotation r = ref Unsafe.Add(ref pointerRotation, entityIndex);

                Vector2 targetDirection = (tm.value - p.value).Normalized();
                d.value = targetDirection;
                Vector2 movement = d.value * v.value * _deltaTime;

                //if (!entity.Has<PendingTransform>() && r.value != Mathf.RadToDeg(targetDirection.Angle()))
                //{
                //    r.value = Mathf.RadToDeg(targetDirection.Angle());
                //    _commandBuffer.Add<PendingTransform>(entity);
                //}
                Vector2 movementNext = p.value + movement;

                var entityInternal = c.rect.Size;
                var resultList = CollisionManager.dynamicCollidersEntities.GetPossibleQuadrants(movementNext, entityInternal.X);
                bool existCollision = false;
                foreach (var itemMap in resultList)
                {
                    foreach (var item in itemMap.Value)
                    {
                        if (item.Key != entity.Id)
                        {
                            Entity entB = item.Value;
                            var entityExternal = entB.Get<Collider>().rect;
                            var entityExternalPos = entB.Get<Position>().value;
                            var direction = entB.Get<Direction>().value;
                            if (CollisionManager.CheckAABBCollision(movementNext, c.rect, entityExternalPos, entityExternal))
                            {
                                existCollision = true;
                                break;
                            }
                        }
                    }
                    if (existCollision)
                    {
                        break;
                    }
                }


                if (!existCollision)
                {
                    // 4. Actualizar la posición de la entidad
                    p.value += movement;

                    // 5. Verificar si la entidad ha llegado al objetivo
                    float distanceToTarget = (tm.value - p.value).Length();
                    if (distanceToTarget <= 1)
                    {
                        _commandBuffer.Remove<TargetMovement>(entity);
                    }
                }

            }
        }
    }

    private readonly struct JobUpdate : IForEachWithEntity<Position, Velocity, TargetMovement, Direction, Collider,Rotation>
    {
        private readonly float _deltaTime;
        private readonly CommandBuffer _commandBuffer;

        public JobUpdate(float deltaTime, CommandBuffer commandBuffer)
        {
            _deltaTime = deltaTime;
            _commandBuffer = commandBuffer;

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update( Entity entity, ref Position p, ref Velocity v, ref TargetMovement tm, ref Direction dir, ref Collider c, ref Rotation r)
        {
            
            Vector2 targetDirection = (tm.value - p.value).Normalized();            
            dir.value = targetDirection;            
            Vector2 movement = dir.value * v.value * _deltaTime;

            if (!entity.Has<PendingTransform>() && r.value != Mathf.RadToDeg(dir.value.Angle()))
            {
                r.value = Mathf.RadToDeg(dir.value.Angle());
                _commandBuffer.Add<PendingTransform>(entity);
            }
            Vector2 movementNext = p.value + movement;
     
            var entityInternal = c.rect.Size;
            var resultList =CollisionManager.dynamicCollidersEntities.GetPossibleQuadrants(movementNext, entityInternal.X);            
            bool existCollision = false;
            foreach (var itemMap in resultList)
            {
                foreach (var item in itemMap.Value)
                {
                    if (item.Key != entity.Id)
                    {
                        Entity entB = item.Value;                        
                            var entityExternal = entB.Get<Collider>().rect;
                            var entityExternalPos = entB.Get<Position>().value;
                            var direction = entB.Get<Direction>().value;
                            if (CollisionManager.CheckAABBCollision(movementNext, c.rect, entityExternalPos, entityExternal))
                            {
                                existCollision = true;
                                break;
                            }                      
                    }
                }

            }
            
            
            if (!existCollision)
            {
                // 4. Actualizar la posición de la entidad
                p.value += movement;

                // 5. Verificar si la entidad ha llegado al objetivo
                float distanceToTarget = (tm.value - p.value).Length();
                if (distanceToTarget <= 1)
                {
                    _commandBuffer.Remove<TargetMovement>(entity);
                }
            }
          
        }

    }

    public override void Update(in float t)
    {
        
        //var job = new JobUpdate((float)t, commandBuffer);
        //World.InlineParallelEntityQuery<JobUpdate,Position, Velocity, TargetMovement, Direction, Collider,Rotation>(in query, ref job);
        World.InlineParallelChunkQuery(in query, new ChunkJobMovement(commandBuffer, t));
        commandBuffer.Playback(World);
    }
}

