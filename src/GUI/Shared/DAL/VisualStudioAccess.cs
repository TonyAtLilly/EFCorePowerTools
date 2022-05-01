﻿namespace EFCorePowerTools.DAL
{
    using Community.VisualStudio.Toolkit;
    using EFCorePowerTools.Helpers;
    using Microsoft.VisualStudio.Data.Services;
    using Microsoft.VisualStudio.Shell;
    using RevEng.Common;
    using Common.DAL;
    using Common.Models;

    public class VisualStudioAccess : IVisualStudioAccess
    {
        private readonly EFCorePowerToolsPackage _package;

        public VisualStudioAccess(EFCorePowerToolsPackage package)
        {
            _package = package;
        }

        DatabaseConnectionModel IVisualStudioAccess.PromptForNewDatabaseConnection()
        {
            var info = VsDataHelper.PromptForInfo(_package);
            if (info.DatabaseType == DatabaseType.Undefined)
                return null;

            return new DatabaseConnectionModel
            {
                ConnectionName = info.ConnectionName,
                ConnectionString = info.ConnectionString,
                DatabaseType = info.DatabaseType,
                DataConnection = info.DataConnection,
            };
        }

        DatabaseDefinitionModel IVisualStudioAccess.PromptForNewDatabaseDefinition()
        {
            var fileName = VsDataHelper.PromptForDacpac();

            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            return new DatabaseDefinitionModel
            {
                FilePath = fileName,
            };
        }

        void IVisualStudioAccess.RemoveDatabaseConnection(IVsDataConnection dataConnection)
        {
            VsDataHelper.RemoveDataConnection(_package, dataConnection);
        }

        void IVisualStudioAccess.ShowMessage(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            VSHelper.ShowMessage(message);
        }

        async System.Threading.Tasks.Task IVisualStudioAccess.StartStatusBarAnimationAsync()
        {
            await VS.StatusBar.StartAnimationAsync(StatusAnimation.Build);
        }

        async System.Threading.Tasks.Task IVisualStudioAccess.StopStatusBarAnimationAsync()
        {
            await VS.StatusBar.EndAnimationAsync(StatusAnimation.Build);
        }

        async System.Threading.Tasks.Task IVisualStudioAccess.SetStatusBarTextAsync(string text)
        {
            await VS.StatusBar.ShowMessageAsync(text);            
        }

        void IVisualStudioAccess.ShowError(string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            VSHelper.ShowError(error);
        }

        void IVisualStudioAccess.OpenFile(string fileName)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate {
                // Switch to main thread
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await VS.Documents.OpenAsync(fileName);
            });
            
        }
    }
}