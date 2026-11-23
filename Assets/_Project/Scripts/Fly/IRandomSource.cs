namespace FruitFlyParenting.Fly
{
    // 결정적 재현성을 위한 난수 추상화. FlyAgent는 GameState.Current.Random(RunSeed 기반)을 감싸서 쓰고,
    // EditMode 테스트는 가짜 구현을 주입해 결과를 고정할 수 있다.
    public interface IRandomSource
    {
        int NextInt(int exclusiveMax);
    }

    public class SystemRandomSource : IRandomSource
    {
        readonly System.Random random;

        public SystemRandomSource(System.Random random)
        {
            this.random = random;
        }

        public int NextInt(int exclusiveMax) => random.Next(exclusiveMax);
    }
}
