using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Bot.Connector;


namespace XamarinShoppingBot
{
    [LuisModel("[LUIS API Key]", "[LUIS API Secret]")]
    [Serializable]
    public class ShoppingDialog : LuisDialog<object>
    {
        string userIssue;
        List<string> customerSupportKeywords = new List<string>(new string[] { "password", "cancel", "login", "log", "create" });

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            string message = "Sorry, I do not understand. Try asking me for order information or customer service help";
            await context.PostAsync(message);
            context.Wait(MessageReceived);
            context.Done(true);
        }

        #region order
        [LuisIntent("OrderStatus")]
        public async Task OrderStatus(IDialogContext context, LuisResult result)
        {
            var entities = result.Entities;
            bool hasOrderNumber = false;
            int orderNumber = 0;

            foreach (var entity in entities)
            {
                if (entity.Type == "OrderNumber")
                {
                    if (int.TryParse(entity.Entity, out orderNumber))
                    {
                        hasOrderNumber = true;
                    }
                    else
                    {
                        PromptDialog.Text(context, OrderNumberAdded, "Please enter a valid order number");
                    }
                }
            }

            if (hasOrderNumber == true)
            {

                if (IsOdd(Convert.ToInt32(orderNumber)))
                {
                    await context.PostAsync("It looks like order " + orderNumber.ToString() + " will be delieved " + DateTime.Now.AddDays(2));
                }
                else
                {
                    await context.PostAsync("Hooray! It looks like " + orderNumber.ToString() + " is scheduled for delivery today!");
                }
            }
            else
            {
                PromptDialog.Text(context, OrderNumberAdded, "Please enter a valid order number");
            }
        }


        private async Task OrderNumberAdded(IDialogContext context, IAwaitable<string> result)
        {
            var orderNumber = await result;
            var deliveryDate = checkOrderStatus(Convert.ToInt32(orderNumber));

            if(deliveryDate.Date == DateTime.Now.Date)
            {
                await context.PostAsync("It looks like your order will be delivered today!");
                context.Done(result);
            }
            else
            {
                await context.PostAsync("Your order is scheduled for delivery on " + deliveryDate.ToLongDateString());
                context.Done(result);
            }
                
        }

        private DateTime checkOrderStatus(int orderNumber)
        {
            if (IsOdd(Convert.ToInt32(orderNumber)))
            {
                return DateTime.Now.AddDays(2);
            }
            else
            {
                return DateTime.Now;
            }
        }

        #endregion

        [LuisIntent("CustomerService")]
        public async Task customerServiceRequest(IDialogContext context, LuisResult result)
        {
            foreach(var entity in result.Entities)
            {
                if (entity.Type == "ServiceKeyword" && customerSupportKeywords.Contains(entity.Entity.ToLower()))
                {
                    switch(entity.Entity.ToLower())
                    {
                        case "password":
                            PromptDialog.Text(context, SupportUsernameEntered, "What is the email address associated with your account?");
                            break;
                        case "cancel":
                            PromptDialog.Text(context, SupportCancelOrderEntered, "Please enter order information");
                            break;
                        case "login":
                            PromptDialog.Text(context, SupportLoginError, "Please provide the username that is associated with your account");
                            break;
                        case "log":
                            PromptDialog.Text(context, SupportLoginError, "Please provide the username that is associated with your account");
                            break;
                        case "sign":
                            await context.PostAsync("You can sign up for an account at https://xamarin.com");
                            context.Done(true);
                            break;
                        case "create":
                            await context.PostAsync("You can create an account at https://xamarin.com");
                            context.Done(true);
                            break;
                    }       
                }
            }
        }

        private async Task SupportLoginError(IDialogContext context, IAwaitable<string> result)
        {
            var item = await result;

            if(IsOdd(item.Length))
            {
                PromptDialog.Choice(context, ContactMethodSelected, new string[] { "Phone", "SMS" },"We need to verify your account, please select a contact method");
            }
            else
            {
                await context.PostAsync("Account was not found, please sign up for a new account");
                context.Done(true);
            }
        }

        private async Task ContactMethodSelected(IDialogContext context, IAwaitable<string> result)
        {
            var method = await result;
            await context.PostAsync("We will contact you via " + method + " to verify your login account");
        }

        private Task SupportCancelOrderEntered(IDialogContext context, IAwaitable<string> result)
        {
            throw new NotImplementedException();
        }

        private async Task SupportUsernameEntered(IDialogContext context, IAwaitable<string> result)
        {
            var item = await result;
            {
                if (!IsOdd(item.Length))
                {
                    await context.PostAsync("Account was not found, please sign up for a new account");
                    context.Done(true);
                }
                else
                {
                    await context.PostAsync("Thanks! We have sent a reset link to your email address!");
                    context.Done(true);
                }
            }
        }

        private async Task AfterDescriptionIssues(IDialogContext context, IAwaitable<string> result)
        {
            userIssue = await result;
            PromptDialog.Choice(context, AfterUserHasChosenAsync, new string[] { "Call", "Text", "SMS" }, "How would you like to be contacted?");
        }

        private async Task AfterUserHasChosenAsync(IDialogContext context, IAwaitable<string> result)
        {
            string userChoice = await result;

            switch (userChoice)
            {
                case "Call":
                    PromptDialog.Number(context, UserProvidesNumber, "What is your phone number?", "Please enter a valid phone number", 3);
                    break;
                case "Text":
                    await context.PostAsync("Let's start a chat session!");
                    break;
                case "SMS":
                    await context.PostAsync("Please text us at 503-555-5555");
                    break;
            }

            // context.Wait(MessageReceived);
        }

        private async Task UserProvidesNumber(IDialogContext context, IAwaitable<long> result)
        {
            long phoneNumber = await result;
            await context.PostAsync("We will call you at " + phoneNumber.ToString());

            context.Wait(MessageReceived);
        }


        public static bool IsOdd(int value)
        {
            return value % 2 != 0;
        }

    }

}