// Re-export from Shared.Contracts so existing Users.Application code keeps compiling.
// The canonical definition now lives in Shared.Contracts.Common.IRequestContextProvider.
global using IRequestContextProvider = Shared.Contracts.Common.IRequestContextProvider;
