"""Integration checks against a running, freshly seeded Development API.
Run: python3 tests/api_smoke.py (restart API before repeating).
Only Python standard library is required.
"""
import concurrent.futures
import http.cookiejar
import json
import os
import unittest
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE=os.environ.get('COMMERCE_API','http://localhost:5000')
class Client:
    def __init__(self):
        self.jar=http.cookiejar.CookieJar()
        self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar))
    def request(self,path,method='GET',body=None,headers=None,csrf=True):
        headers={'Content-Type':'application/json',**(headers or {})}
        if csrf:
            for cookie in self.jar:
                if cookie.name=='XSRF-TOKEN':headers['X-XSRF-TOKEN']=urllib.parse.unquote(cookie.value)
        request=urllib.request.Request(BASE+'/api/'+path,data=json.dumps(body).encode() if body is not None else None,headers=headers,method=method)
        try:
            with self.opener.open(request,timeout=30) as response:
                data=response.read();return response.status,json.loads(data) if data else None
        except urllib.error.HTTPError as error:
            data=error.read()
            try:data=json.loads(data)
            except ValueError:data=data.decode()
            return error.code,data
    def csrf(self):return self.request('auth/csrf')
    def login(self,admin=False):
        self.csrf()
        status,result=self.request('auth/login','POST',{'email':'admin@commerce.test' if admin else 'customer@commerce.test','password':'AdminDemo123!' if admin else 'CustomerDemo123!'})
        assert status==200,(status,result)
        self.csrf();return result
    def register(self):
        self.csrf();email=str(uuid.uuid4())+'@example.test'
        result=self.request('auth/register','POST',{'name':'Test Customer','email':email,'password':'TestCustomer123!'})
        self.csrf();return result

class CommerceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.admin=Client();cls.admin.login(True)
        cls.customer=Client();cls.customer.login()
    def product(self,stock=4):
        status,p=self.admin.request('admin/products','POST',{'name':'Test '+str(uuid.uuid4())[:8],'description':'Integration test product','category':'Test','price':12.50,'stockQuantity':stock,'imageUrl':'','active':True})
        self.assertEqual(status,201,p);return p
    def checkout(self,p,client=None,key=None,quantity=1,extra=None):
        payload={'items':[{'productId':p['id'],'quantity':quantity}],'shipping':{'name':'Demo Buyer','line1':'100 Test Lane','city':'Houston','postalCode':'77001','country':'US'},**(extra or {})}
        return (client or self.customer).request('orders/checkout','POST',payload,{'Idempotency-Key':key or str(uuid.uuid4())})
    def test_01_anonymous_cannot_browse(self):self.assertEqual(Client().request('products')[0],401)
    def test_02_login_requires_csrf(self):
        self.assertEqual(Client().request('auth/login','POST',{'email':'admin@commerce.test','password':'AdminDemo123!'},csrf=False)[0],400)
    def test_03_wrong_password(self):
        c=Client();c.csrf();self.assertEqual(c.request('auth/login','POST',{'email':'admin@commerce.test','password':'wrong'})[0],401)
    def test_04_registration_and_duplicate(self):
        c=Client();status,user=c.register();self.assertEqual(status,200);self.assertEqual(user['role'],'Customer')
        self.assertEqual(c.request('auth/register','POST',{'name':'Another','email':user['email'],'password':'TestCustomer123!'})[0],409)
        self.assertNotIn('passwordHash',user)
    def test_05_customer_cannot_admin(self):self.assertEqual(self.customer.request('admin/products')[0],403)
    def test_06_product_validation(self):
        self.assertEqual(self.admin.request('admin/products','POST',{'name':'Bad','description':'test','category':'Test','price':-1,'stockQuantity':2,'imageUrl':''})[0],400)
    def test_07_add_edit_deactivate(self):
        p=self.product();p['name']='Updated';self.assertEqual(self.admin.request('admin/products/'+p['id'],'PUT',p)[0],200)
        self.assertEqual(self.customer.request('products/'+p['id'])[1]['name'],'Updated')
        self.assertEqual(self.admin.request('admin/products/'+p['id'],'DELETE',{})[0],204)
        self.assertEqual(self.customer.request('products/'+p['id'])[0],404)
    def test_08_authoritative_total_and_idempotency(self):
        p=self.product();key=str(uuid.uuid4());status,result=self.checkout(p,key=key,quantity=2,extra={'total':0.01})
        self.assertEqual(status,200,result)
        status,retry=self.checkout(p,key=key,quantity=2,extra={'total':0.01});self.assertEqual(status,200);self.assertEqual(result['orderId'],retry['orderId'])
        order=next(o for o in self.customer.request('orders')[1] if o['id']==result['orderId']);self.assertEqual(order['total'],25)
        self.assertEqual(self.customer.request('products/'+p['id'])[1]['stockQuantity'],2)
        self.assertEqual(self.checkout(p,key=key,quantity=1)[0],409)
    def test_09_failure_releases_stock_once(self):
        p=self.product();_,result=self.checkout(p,quantity=2);path='orders/'+result['orderId']+'/simulate'
        self.assertEqual(self.customer.request(path,'POST',{'outcome':'Failed'})[0],200)
        self.customer.request(path,'POST',{'outcome':'Failed'})
        self.assertEqual(self.customer.request('products/'+p['id'])[1]['stockQuantity'],4)
    def test_10_paid_stays_paid_and_fulfillment(self):
        p=self.product();_,result=self.checkout(p);id=result['orderId'];path='orders/'+id+'/simulate'
        self.assertEqual(self.customer.request(path,'POST',{'outcome':'Paid'})[1]['paymentStatus'],'Paid')
        self.assertEqual(self.customer.request(path,'POST',{'outcome':'Failed'})[1]['paymentStatus'],'Paid')
        self.assertEqual(self.admin.request('admin/orders/'+id+'/fulfillment','PUT',{'status':'Delivered'})[0],409)
        for status in ['Processing','Shipped','Delivered']:self.assertEqual(self.admin.request('admin/orders/'+id+'/fulfillment','PUT',{'status':status})[0],204)
    def test_11_foreign_orders_protected(self):
        p=self.product();_,result=self.checkout(p);other=Client();self.assertEqual(other.register()[0],200)
        self.assertEqual(other.request('orders/'+result['orderId']+'/simulate','POST',{'outcome':'Paid'})[0],404)
        self.assertFalse(any(o['id']==result['orderId'] for o in other.request('orders')[1]))
    def test_12_invalid_quantities_and_stock(self):
        p=self.product(stock=1);self.assertEqual(self.checkout(p,quantity=2)[0],409);self.assertEqual(self.checkout(p,quantity=-1)[0],400)
    def test_13_parallel_stock_reservation(self):
        p=self.product(stock=1);other=Client();self.assertEqual(other.register()[0],200)
        with concurrent.futures.ThreadPoolExecutor(2) as pool:
            futures=[pool.submit(self.checkout,p,c) for c in [self.customer,other]]
            self.assertEqual(sorted(f.result()[0] for f in futures),[200,409])
    def test_14_disable_revokes_session(self):
        c=Client();_,user=c.register()
        self.assertEqual(self.admin.request('admin/users/'+user['id'],'PUT',{'role':'Customer','active':False})[0],204)
        self.assertEqual(c.request('products')[0],401)
    def test_15_admin_cannot_disable_self(self):
        user=self.admin.request('auth/me')[1]
        self.assertEqual(self.admin.request('admin/users/'+user['id'],'PUT',{'role':'Customer','active':False})[0],400)
    def test_16_simulated_mode_rejects_webhook(self):
        self.assertEqual(Client().request('payments/stripe/webhook','POST',{},csrf=False)[0],404)
    def test_17_logout_revokes_browser_cookie(self):
        c=Client();c.register();self.assertEqual(c.request('auth/logout','POST',{})[0],204);self.assertEqual(c.request('products')[0],401)
    def test_18_csrf_required_for_admin_write(self):
        self.assertEqual(self.admin.request('admin/products','POST',{},csrf=False)[0],400)

if __name__=='__main__':unittest.main(verbosity=2)
