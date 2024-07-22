using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharedWebComponents.Services;
public class DeleteService
{
    [Inject] public required ApiClient Client { get; set; }

    /*private static DeleteService s_instance;
    private static readonly object s_lock = new object();

    public static DeleteService Instance
    {
        get
        {
            lock (s_lock)
            {
                if (s_instance == null)
                {
                    s_instance = new DeleteService();
                }
                return s_instance;
            }
        }
    }*/

    private bool _isDeletingDocuments = false;
    private int _deleteProgress;

    public event Action OnChange;
    public event Action<int> OnDeleteProgressChanged; // Event to notify when _deleteProgress is updated
    public event Action<bool> OnIsDeletingChanged; // Event to notify when _isDeleting is updated

    public void UpdateDeleteProgress(int progress)
    {
        _deleteProgress = progress;
        OnDeleteProgressChanged?.Invoke(_deleteProgress);
        NotifyStateChanged();
    }

    public void UpdateIsDeleting(bool isDeleting)
    {
        _isDeletingDocuments = isDeleting;
        OnIsDeletingChanged?.Invoke(_isDeletingDocuments);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public int getDeleteProgress()
    {
        return _deleteProgress;
    }
    public bool getIsDeleting()
    {
        return _isDeletingDocuments;
    }
}
