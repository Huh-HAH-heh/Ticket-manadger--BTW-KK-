using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ticket_manadger
{
    internal class RequestRepo
    {
        public List<Request> GetAllRequest()
        {
            using (var db = new AppDbContext())
            {
                return db.Requests.Include(r => r.Status).Include(r => r.Client).ToList();
            }
        }
        public void AddRequest(string title, string Description,int clientId)
        {
            using (var db = new AppDbContext())
            {
                //return db.Requests.Include(r => r.Status).Include(r => r.Client).ToList();
                var newRequest = new Request {
                    Title = title,
                    description = Description,
                    StatusId = 1,
                    ClientId = clientId
            };
                db.Requests.Add(newRequest);
                db.SaveChanges();

            }
        }
        public bool UpdateStatus(int requestId,int statusId)
        {
            using (var db = new AppDbContext())
            {
                var request = db.Requests.Find(requestId);
                if (request == null)
                {
                    return false;
                }
                request.StatusId = statusId;
                db.SaveChanges();return true;

            }
        }
        public bool DelRequest(int requestId)
        {
            using (var db = new AppDbContext())
            {
                var request = db.Requests.Find(requestId);
                if (request == null)
                {
                    return false;
                }
                db.Requests.Remove(request);
                db.SaveChanges(); return true;

            }
        }
        public Dictionary<string,int> GetLog()
        {
            using (var db = new AppDbContext())
            {
                return db.Requests.Include(r => r.Status).GroupBy(r => r.Status.Name).ToDictionary(f => f.Key,f=>f.Count());
            }
        }

    }
}
