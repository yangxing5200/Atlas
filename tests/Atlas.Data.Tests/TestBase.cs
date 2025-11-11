// TestBase.cs
using AutoFixture;
using AutoFixture.Xunit2;

namespace Atlas.Data.Tests
{
    public abstract class TestBase
    {
        protected IFixture Fixture { get; }

        protected TestBase()
        {
            Fixture = new Fixture();
            ConfigureFixture(Fixture);
        }

        protected virtual void ConfigureFixture(IFixture fixture)
        {
            // Override in derived classes if needed
        }
    }

    public class AutoMoqDataAttribute : AutoDataAttribute
    {
        public AutoMoqDataAttribute()
            : base(() => new Fixture())
        {
        }
    }
}