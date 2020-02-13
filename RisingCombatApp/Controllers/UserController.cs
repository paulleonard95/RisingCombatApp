using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using RisingCombatApp.Models;

namespace RisingCombatApp.Controllers
{
    public class UserController : Controller
    {

        //Registration Action
        [HttpGet]
        public ActionResult Registration()
        {
            return View();
        }

        //Registration POST action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Registration([Bind(Exclude = "IsEmailVerified,ActivationCode")]User user)
        {
            bool Status = false;
            string message = "";

            //Model Validation
            if(ModelState.IsValid)
            {
                //Email is already Exist
                #region //Email is already Exit
                var isExist = IsEmailExist(user.EmailID);
                if(isExist)
                {
                    ModelState.AddModelError("EmailExist", "Email already exist");
                    return View(user);
                }
                #endregion

                #region //Generate Activation Code
                user.ActivationCode = Guid.NewGuid();
                #endregion

                #region //Password Hashing
                user.Password = Crypto.Hash(user.Password);
                user.ConfirmPassword = Crypto.Hash(user.ConfirmPassword);
                #endregion

                user.IsEmailVerified = false;

                #region Save to Database
                using (RaisingCombatDBEntities dc = new RaisingCombatDBEntities())
                {
                    dc.Users.Add(user);
                    dc.SaveChanges();

                    //Send Email to User
                    SendVerificationLinkEmail(user.EmailID, user.ActivationCode.ToString());
                    message = "Registration successfully done. Account activation link " +
                        "has been sent to your email id: " + user.EmailID;
                    Status = true;
                }
                #endregion
            }
            else
            {
                message = "Invalid Request";
            }

            ViewBag.Message = message;
            ViewBag.Status = Status;
            return View(user);
        }

        //Verify Account
        [HttpGet]
        public ActionResult VerifyAccount(string id)
        {
            bool Status = false;
            using (RaisingCombatDBEntities dc = new RaisingCombatDBEntities())
            {
                dc.Configuration.ValidateOnSaveEnabled = false; // This line is to avoid confirm password does not match issue on save changes
                var v = dc.Users.Where(a => a.ActivationCode == new Guid(id)).FirstOrDefault();
                if(v != null)
                {
                    v.IsEmailVerified = true;
                    dc.SaveChanges();
                    Status = true;
                }
                else
                {
                    ViewBag.Message = "Invalid Request";
                }
            }
            ViewBag.Status = Status;
            return View();
        }

        //Login 
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        //Login POST
        [HttpPost]
        public ActionResult Login(UserLogin login, string ReturnUrl="")
        {
            string message = "";
            using (RaisingCombatDBEntities dc = new RaisingCombatDBEntities())
            {
                var v = dc.Users.Where(a => a.EmailID == login.EmailID).FirstOrDefault();
                if(v != null)
                {
                    if(string.Compare(Crypto.Hash(login.Password), v.Password) == 0)
                    {
                        int timeout = login.RememberMe ? 525600 : 20; // 525600 minutes = 1 year
                        var ticket = new FormsAuthenticationTicket(login.EmailID, login.RememberMe, timeout);
                        string encrypted = FormsAuthentication.Encrypt(ticket);
                        var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted);
                        cookie.Expires = DateTime.Now.AddMinutes(timeout);
                        cookie.HttpOnly = true;
                        Response.Cookies.Add(cookie);

                        if(Url.IsLocalUrl(ReturnUrl))
                        {
                            return Redirect(ReturnUrl);
                            
                        }
                        else
                        {
                            return RedirectToAction("Index", "Home");
                        }
                    }
                    else
                    {
                        message = "Invalid credentials provided";
                    }
                }
                else
                {
                    message = "Invalid credentials provided";
                }
            }
            ViewBag.Message = message;
            return View();
        }

        //Logout
        [Authorize]
        [HttpPost]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "User");
        }

        [NonAction]
        public bool IsEmailExist(string emailID)
        {
            using(RaisingCombatDBEntities dc = new RaisingCombatDBEntities())
            {
                var v = dc.Users.Where(a => a.EmailID == emailID).FirstOrDefault();
                return v != null;
            }
        }

        [NonAction]
        public void SendVerificationLinkEmail(string emailID, string activationCode, string emailFor = "VerifyAccount")
        {
            var verifyUrl = "/User/"+emailFor+"/" + activationCode;
            var link = Request.Url.AbsoluteUri.Replace(Request.Url.PathAndQuery, verifyUrl);

            var fromEmail = new MailAddress("paulleonard95@gmail.com", "Raising Combat");
            var toEmail = new MailAddress(emailID);
            var fromEmailPassword = "derrycity9"; //Replace with actual password
            string subject = "";
            string body = "";

            if(emailFor == "VerifyAccount")
            {
                subject = "Your account is successfully created!";

                body = "<br/><br/>We are excited to tell you that your Raising Combat account has " +
                    " been set up and ready to use! Please click on the below link to verify your account" +
                    " <br/><br/><a href='" + link + "'>" + link + "</a> ";
            }
            else if (emailFor == "ResetPassword")
            {
                subject = "Reset Password";
                body = "Hi, <br/><br/>We got a request to reset your account password. Please click on the link below to reset your password" +
                    "<br/><br/><a href=" +link+">Reset Password Link</a>";
            }

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromEmail.Address, fromEmailPassword)
            };

            using (var message = new MailMessage(fromEmail, toEmail)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
                smtp.Send(message);
        }

        //Forgot Password
        public ActionResult ForgotPassword()
        {
            return View();
        }

        //Verify Forgot Password Email
        [HttpPost]
        public ActionResult ForgotPassword(string EmailID)
        {
            //Verify Emial ID
            string message = "";
            bool status = false;

            using (RaisingCombatDBEntities dc = new RaisingCombatDBEntities())
            {
                var account = dc.Users.Where(a => a.EmailID == EmailID).FirstOrDefault();
                if (account != null)
                {
                    // Send email for reset password and generate reset password link then send email
                    string resetCode = Guid.NewGuid().ToString();
                    SendVerificationLinkEmail(account.EmailID, resetCode, "ResetPassword");
                    account.ResetPasswordCode = resetCode;
                    //This line is to avoid confirm password not match issue - as confirm password property added in current model class
                    dc.Configuration.ValidateOnSaveEnabled = false;
                    //Update Database
                    dc.SaveChanges();
                    message = "Reset Password link has been sent to your email address";
                }
                else
                {
                    message = "Account does not exist. Please register!";
                }
            }
            ViewBag.Message = message;
            return View();
        }

        public ActionResult ResetPassword(string id)
        {
            //Verify the reset password link
            //Find account assoiciated with this link
            //redirect to reset password page
            using(RaisingCombatDBEntities dc = new RaisingCombatDBEntities())
            {
                //Take reset password code from database
                var user = dc.Users.Where(a => a.ResetPasswordCode == id).FirstOrDefault();
                //If code found matches id then go into if statement
                if(user != null)
                {
                    //Then goes into the view
                    ResetPasswordModel model = new ResetPasswordModel();
                    model.ResetCode = id;
                    return View(model);
                }
                else
                {
                    return HttpNotFound();
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordModel model)
        {
            var message = "";
            if(ModelState.IsValid)
            {
                using(RaisingCombatDBEntities dc = new RaisingCombatDBEntities())
                {
                    var user = dc.Users.Where(a => a.ResetPasswordCode == model.ResetCode).FirstOrDefault();
                    if(user != null)
                    {
                        user.Password = Crypto.Hash(model.NewPassword);
                        // reset password code is set to empty as can only be used once
                        user.ResetPasswordCode = "";
                        // to stop confirm password issue
                        dc.Configuration.ValidateOnSaveEnabled = false;
                        dc.SaveChanges();
                        message = "New Password updated successfully";
                    }
                }
            }
            else
            {
                message = "Something is invalid";
            }

            ViewBag.Message = message;
            return View(model);
        }
    }

}