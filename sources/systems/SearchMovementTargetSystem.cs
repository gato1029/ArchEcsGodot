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
public struct SearchTarget
{
}
internal class SearchMovementTargetSystem : BaseSystem<World, float>
{

    private CommandBuffer commandBuffer;
    private QueryDescription query = new QueryDescription().WithAll<Unit, IAController,SearchTarget>();
    public SearchMovementTargetSystem(World world) : base(world)
    {
        commandBuffer = new CommandBuffer();
    }


    private struct ChunkJob : IChunkJob
    {
        private readonly float _deltaTime;
        private readonly CommandBuffer _commandBuffer;
        private readonly RandomNumberGenerator rng;
        public ChunkJob(CommandBuffer commandBuffer, float deltaTime) : this()
        {
            _commandBuffer = commandBuffer;
            _deltaTime = deltaTime;
            rng = new RandomNumberGenerator();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Execute(ref Chunk chunk)
        {
            ref var pointerEntity = ref chunk.Entity(0);
            ref var pointerAreaMovement = ref chunk.GetFirst<AreaMovement>();
            ref var pointerPosition = ref chunk.GetFirst<Position>();
            foreach (var entityIndex in chunk)
            {
                ref Entity entity = ref Unsafe.Add(ref pointerEntity, entityIndex);
                ref AreaMovement am = ref Unsafe.Add(ref pointerAreaMovement, entityIndex);
                ref Position p = ref Unsafe.Add(ref pointerPosition, entityIndex);
                Vector2 point = p.value;
                switch (am.type)
                {
                    case MovementType.CIRCLE:
                        point = MovementCircle(rng, p.value, am.value);
                        break;
                    case MovementType.SQUARE:
                        point = MovementSquare(rng, p.value, am.value, am.value2);
                        break;
                    default:
                        break;
                }
                if (!entity.Has<TargetMovement>())
                {
                    _commandBuffer.Add<TargetMovement>(entity, new TargetMovement { value = point });
                }

            }
        }
    }
    public override void Update(in float t)
    {
        World.InlineParallelChunkQuery(in query, new ChunkJob(commandBuffer, t));
        commandBuffer.Playback(World);
    }
    static Vector2 MovementCircle(RandomNumberGenerator rng, Vector2 origin, uint radius)
    {
        
        float angle = rng.Randf() * Mathf.Pi * 2; 

        float distance = Mathf.Sqrt(rng.Randf()) * radius; 

        float x = Mathf.Cos(angle) * distance;
        float y = Mathf.Sin(angle) * distance;

        Vector2 vector2 = origin + new Vector2(x, y);

        return vector2;

    }
    static Vector2 MovementSquare(RandomNumberGenerator rng, Vector2 origin, uint height, uint width)
    {
        float x = rng.Randf() * width; 
        float y = rng.Randf() * height; 

        Vector2 vector2 = origin + new Vector2(x, y);

        return vector2;

    }
}
