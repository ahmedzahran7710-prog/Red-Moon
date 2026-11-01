using Canyon.Game.States.User;

namespace Canyon.Game.States.MessageBoxes
{
    public sealed class CleanInventoryMessageBox : MessageBox
    {
        private static readonly ILogger logger = LogFactory.CreateGmLogger("clear_inventory");

        public CleanInventoryMessageBox(Character user) 
            : base(user)
        {
            Message = StrClearInventoryConfirmation;
            TimeOut = 30;
        }

        public override async Task OnAcceptAsync()
        {
            if (!user.IsUnlocked())
            {
                await user.SendSecondaryPasswordInterfaceAsync();
                return;
            }

            if (user.Trade != null || user.Booth != null)
            {
                return;
            }

            logger.LogInformation("User [{},{}] has accepted to clean inventory!", user.Identity, user.Name);
            await user.UserPackage.ClearInventoryAsync();
        }
    }
}
