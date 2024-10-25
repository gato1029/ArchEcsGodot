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

[Component]
public struct ColliderMelleAtack
{
    public Rect2 rect;
    public Vector2 offset;
    public Rect2 rectTransform;
}

[Component]
public struct RangeAttack
{
    public int area;    
}

[Component]
public struct FrecuencyAttack
{
    public float value;
    public float timeAccumulator;
}

[Component]
public struct PendingAttack
{
    public Entity entityTarget; 
    public int damage;   
}
[Component]
public struct Health
{
    public int value;    
}
[Component]
public struct Shield
{
    public int value;
}
[Component]
public struct Resistance
{
    public int fire;
    public int water;
    public int earth;
    public int air;
}
[Component]
public struct Damage
{
    public int value;
}
[Component]
public struct PendingRemove
{    
}

internal class AtackManager : BaseSystem<World, float>
{
    private CommandBuffer commandBuffer;
    private QueryDescription query = new QueryDescription().WithAll<Unit, Position, Direction, Melee, ColliderMelleAtack, FrecuencyAttack>();
    private QueryDescription queryPending = new QueryDescription().WithAll<Unit, Melee, PendingAttack>();
 
    public AtackManager(World world) : base(world)
    {
        commandBuffer = new CommandBuffer();
    }


    private readonly struct ProcessJobMelee : IForEachWithEntity<Position, Direction, Damage, ColliderMelleAtack, FrecuencyAttack>
    {
        private readonly float _deltaTime;
        private readonly CommandBuffer _commandBuffer;

        public ProcessJobMelee(float deltaTime, CommandBuffer commandBuffer)
        {
            _deltaTime = deltaTime;
            _commandBuffer = commandBuffer;

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(Entity entity, ref Position pos, ref Direction d, ref Damage da, ref ColliderMelleAtack ca, ref FrecuencyAttack fa)
        {

            Vector2 positionAtack = pos.value+ GetRotatedPointByDirection(ca.offset,Vector2.Zero,d.value);
          
            var result = CollisionManager.dynamicCollidersEntities.GetPossibleQuadrants(positionAtack, ca.offset.X);            
            if (result!=null)
            {
                foreach (var itemDic in result)
                {
                    foreach (var item in itemDic.Value)
                    {
                        if (item.Key != entity.Id)
                        {
                            Entity entB = item.Value;
                            if (entB.Has<Sprite>() && !entB.Has<PendingAttack>())
                            {
                                var sp = entB.Get<Sprite>();
                                var entityExternal = entB.Get<Sprite>().rect.Size;
                                var entityExternalPos = entB.Get<Position>().value;

                                if (CollisionManager.CheckAABBCollision(positionAtack, ca.rect.Size, d.value, entityExternalPos, entityExternal, entB.Get<Direction>().value))
                                {
                                    _commandBuffer.Add<PendingAttack>(entity, new PendingAttack { entityTarget = entB, damage = da.value });
                                }
                            }

                        }
                    }
                }
               
            }


        }

    }

    private readonly struct ProcessJobPendingAtack : IForEachWithEntity<PendingAttack>
    {
        private readonly float _deltaTime;
        private readonly CommandBuffer _commandBuffer;

        public ProcessJobPendingAtack(float deltaTime, CommandBuffer commandBuffer)
        {
            _deltaTime = deltaTime;
            _commandBuffer = commandBuffer;

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(Entity entity, ref PendingAttack pa)
        {
            pa.entityTarget.Get<Health>().value -= pa.damage; 
            Health health = pa.entityTarget.Get<Health>();            
            _commandBuffer.Remove<PendingAttack>(entity);
            if (!pa.entityTarget.Has<PendingRemove>())
            {
                if (health.value <= 0)
                {
                    _commandBuffer.Add<PendingRemove>(pa.entityTarget);
                }
            }
            
        }

    }

  


    public static Vector2 GetRotatedPointByDirection(Vector2 point, Vector2 origin, Vector2 direction)
    {
        // Normalizar la dirección para obtener un vector unitario
        direction = direction.Normalized();

        // Calcular el vector perpendicular para el eje de la rotación
        Vector2 perpDirection = new Vector2(-direction.Y, direction.X);

        // Mover el punto al origen (trasladar)
        Vector2 relativePoint = point - origin;

        // Aplicar la rotación usando la dirección y su perpendicular
        Vector2 rotatedPoint = origin + (relativePoint.X * direction + relativePoint.Y * perpDirection);

        return rotatedPoint;
    }


    public override void Update(in float t)
    {
        
        var job = new ProcessJobMelee((float)t, commandBuffer);
        World.InlineParallelEntityQuery<ProcessJobMelee, Position, Direction, Damage, ColliderMelleAtack, FrecuencyAttack>(in query, ref job);
        commandBuffer.Playback(World);

        var jobPending = new ProcessJobPendingAtack((float)t, commandBuffer);
        World.InlineParallelEntityQuery<ProcessJobPendingAtack, PendingAttack>(in queryPending, ref jobPending);
        commandBuffer.Playback(World);
    }


}

