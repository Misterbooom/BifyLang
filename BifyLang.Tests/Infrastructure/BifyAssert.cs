#nullable enable
using BifyLang.Exceptions;

namespace BifyLang.Tests.Infrastructure;

public static class BifyAssert
{
    public static TError CompileError<TError>(string code) where TError : BifyError
    {
        Traceback.Instance.Clear();
        Traceback.Instance.Catch(typeof(TError));
        try
        {
            try
            {
                using var compiled = Bify.Compile(code);
            }
            catch (ApplicationException)
            {
                // Traceback throws ApplicationException when untracked or in some error paths
            }
            catch (Exception ex) when (ex is not AssertionException)
            {
                // In case a compiler exception cascaded from a caught BifyError returning default
            }

            var err = Traceback.Instance.GetError();
            Assert.That(err, Is.Not.Null, $"Expected error of type '{typeof(TError).Name}', but no error was raised.");
            Assert.That(err, Is.InstanceOf<TError>(), $"Expected error of type '{typeof(TError).Name}', but got '{err?.GetType().Name}': {err?.Message}");
            return (TError)err!;
        }
        finally
        {
            Traceback.Instance.TrackPop();
        }
    }

    public static TError ActionThrows<TError>(Action action, string? expectedSubstring = null) where TError : BifyError
    {
        Traceback.Instance.Clear();
        Traceback.Instance.Catch(typeof(TError));
        try
        {
            try { action(); }
            catch (ApplicationException) { }
            catch (Exception ex) when (ex is not AssertionException) { }

            var err = Traceback.Instance.GetError();
            Assert.That(err, Is.Not.Null, $"Expected error of type '{typeof(TError).Name}', but no error was raised.");
            Assert.That(err, Is.InstanceOf<TError>(), $"Expected error of type '{typeof(TError).Name}', but got '{err?.GetType().Name}': {err?.Message}");
            if (expectedSubstring != null)
                Assert.That(err!.Message, Does.Contain(expectedSubstring));
            return (TError)err!;
        }
        finally
        {
            Traceback.Instance.TrackPop();
        }
    }

    public static TError ParseError<TError>(string code) where TError : BifyError
    {
        Traceback.Instance.Clear();
        Traceback.Instance.Catch(typeof(TError));
        try
        {
            try
            {
                Bify.Parse(code);
            }
            catch (ApplicationException)
            {
                // Expected when Traceback encounters an untracked error or exits
            }
            catch (Exception ex) when (ex is not AssertionException)
            {
            }

            var err = Traceback.Instance.GetError();
            Assert.That(err, Is.Not.Null, $"Expected parse error of type '{typeof(TError).Name}', but no error was raised.");
            Assert.That(err, Is.InstanceOf<TError>(), $"Expected parse error of type '{typeof(TError).Name}', but got '{err?.GetType().Name}': {err?.Message}");
            return (TError)err!;
        }
        finally
        {
            Traceback.Instance.TrackPop();
        }
    }

    public static void AnySyntaxError(string code)
    {
        Traceback.Instance.Clear();
        try
        {
            bool threw = false;
            try
            {
                Bify.Parse(code);
            }
            catch (ApplicationException)
            {
                threw = true;
            }
            catch (Exception)
            {
                threw = true;
            }

            Assert.That(threw, Is.True, "Expected syntax error during parsing, but none occurred.");
        }
        finally
        {
            Traceback.Instance.Clear();
        }
    }
}
