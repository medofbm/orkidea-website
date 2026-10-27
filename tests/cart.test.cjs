const {test}=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const fs=require('node:fs');
const path=require('node:path');
const source=fs.readFileSync(path.join(__dirname,'../Orkideya/wwwroot/js/site.js'),'utf8');
function fixture(variantId, response, fail=false) {
 let handler, requests=[];
 const button={dataset:{variantId,quantity:'2'},innerHTML:'Add',disabled:false,closest:()=>null,
 addEventListener:(event,callback)=>handler=callback};
 const classes=new Set();
 const badge={textContent:'0',classList:{add:x=>classes.add(x),remove:x=>classes.delete(x)}};
 const document={addEventListener:()=>{},querySelectorAll:()=>[button],querySelector:s=>s==='#cartBadge'?badge:{value:'csrf-test'}};
 const context=vm.createContext({document,console:{log(){},error(){}},setTimeout:callback=>callback(),clearTimeout(){},fetch:async(url,options)=>{requests.push({url,options});if(fail)throw Error('offline');return {ok:true,json:async()=>response};}});
 vm.runInContext(source,context);vm.runInContext('initAddToCart()',context);
 return {button,badge,classes,requests,click:()=>handler.call(button,{preventDefault(){}}),context};
}
test('missing variant restores button and sends no request',async()=>{const f=fixture('');await f.click();assert.equal(f.button.innerHTML,'Add');assert.equal(f.button.disabled,false);assert.equal(f.requests.length,0);});
test('successful add sends POST with CSRF and updates badge',async()=>{const f=fixture('7',{success:true,count:3});await f.click();assert.equal(f.requests[0].options.method,'POST');assert.equal(f.requests[0].options.headers.RequestVerificationToken,'csrf-test');assert.equal(f.badge.textContent,3);assert.equal(f.button.disabled,false);assert.equal(f.button.innerHTML,'Add');});
test('network failure restores button',async()=>{const f=fixture('7',null,true);await f.click();assert.equal(f.button.disabled,false);assert.equal(f.button.innerHTML,'Add');});
test('server rejection restores button without changing cart count',async()=>{const f=fixture('7',{success:false,message:'Unavailable'});await f.click();assert.equal(f.badge.textContent,'0');assert.equal(f.button.innerHTML,'Add');});
test('empty cart hides its badge',()=>{const f=fixture('7');vm.runInContext('updateCartBadge(0)',f.context);assert.ok(f.classes.has('cart-badge--hidden'));});

for (const quantity of ['0','-1','100','1.5','abc','2items','9007199254740992']) {
 test(`invalid quantity ${quantity} sends no request and restores button`,async()=>{
  const f=fixture('7');f.button.dataset.quantity=quantity;await f.click();
  assert.equal(f.requests.length,0);assert.equal(f.button.innerHTML,'Add');assert.equal(f.button.disabled,false);
 });
}
for (const variant of ['0','-1','1.5','abc','7items']) {
 test(`invalid variant ${variant} sends no request`,async()=>{
  const f=fixture(variant);await f.click();assert.equal(f.requests.length,0);assert.equal(f.button.disabled,false);
 });
}
test('quantity upper boundary is accepted',async()=>{
 const f=fixture('7',{success:true,count:99});f.button.dataset.quantity='99';await f.click();
 assert.equal(f.requests.length,1);assert.match(f.requests[0].url,/quantity=99$/);
});
