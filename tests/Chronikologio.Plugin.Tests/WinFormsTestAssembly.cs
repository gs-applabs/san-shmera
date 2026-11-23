using Xunit;

// WinForms controls share message loops and theme state; avoid concurrent UI test collections.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
