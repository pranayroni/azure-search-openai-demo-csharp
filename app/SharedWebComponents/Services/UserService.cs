// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedWebComponents.Services;
public class UserService
{
    private string _name;
    private string _email;
    private string _firstname;
    private string _lastname;



    public event Action OnChange;
    public event Action<Guid> UserUpdated; // Event to notify when a chat is updated

    public void UpdateUser(string name, string email, string firstname, string lastname)
    {
        _email = email;
        _name = name;
        _firstname = firstname;
        _lastname = lastname;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
