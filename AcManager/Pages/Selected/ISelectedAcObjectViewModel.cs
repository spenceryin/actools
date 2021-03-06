﻿using System.Windows.Input;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Pages.Selected {
    public interface ISelectedAcObjectViewModel {
        AcCommonObject SelectedAcObject { get; }

        void Load();

        void Unload();

        ICommand FindInformationCommand { get; }

        ICommand ChangeIdCommand { get; }

        ICommand CloneCommand { get; }
    }
}