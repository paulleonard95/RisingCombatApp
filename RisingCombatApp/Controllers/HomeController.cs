using RisingCombatApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace RisingCombatApp.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Timetable()
        {
            return View();
        }

        public JsonResult GetEvents()
        {
            using (RaisingCombatDBEntities dc = new RaisingCombatDBEntities())
            {
                var events = dc.Timetables.ToList();
                return new JsonResult { Data = events, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            }
        }
    }
}