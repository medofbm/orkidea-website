const {test}=require('node:test');
const assert=require('node:assert/strict');
const {initCheckoutSummary}=require('../Orkideya/wwwroot/js/checkout.js');
function fixture(subtotal,price,value='1') {
 let change;
 const elements={checkoutSummary:{dataset:{subtotal}},shippingRateIdInput:{value,selectedOptions:[{dataset:price===undefined?{}:{price}}],addEventListener:(event,callback)=>change=callback},shippingTotal:{textContent:''},orderTotal:{textContent:''}};
 initCheckoutSummary({getElementById:id=>elements[id]});
 return {elements,change};
}
test('shipping is added to subtotal',()=>{const f=fixture('42.50','10.00');assert.equal(f.elements.orderTotal.textContent,'52.50 د.ل');assert.equal(f.elements.shippingTotal.textContent,'10.00 د.ل');});
test('free listed shipping is valid',()=>{assert.equal(fixture('42.50','0').elements.orderTotal.textContent,'42.50 د.ل');});
test('restored city selection calculates immediately',()=>{assert.equal(fixture('10','15').elements.orderTotal.textContent,'25.00 د.ل');});
test('changing the city updates the total',()=>{const f=fixture('10','15');f.elements.shippingRateIdInput.selectedOptions[0].dataset.price='20';f.change();assert.equal(f.elements.orderTotal.textContent,'30.00 د.ل');});
for (const price of [undefined,'','-1','not-a-number','Infinity']) test(`invalid shipping ${price} does not show a final total`,()=>{assert.equal(fixture('10',price).elements.orderTotal.textContent,'اختر المدينة');});
test('unselected city does not imply zero shipping',()=>{assert.equal(fixture('10','0','').elements.orderTotal.textContent,'اختر المدينة');});
test('missing checkout markup is harmless',()=>{assert.doesNotThrow(()=>initCheckoutSummary({getElementById:()=>null}));});

test('clearing city removes the previously calculated total',()=>{const f=fixture('10','15');f.elements.shippingRateIdInput.value='';f.change();assert.equal(f.elements.orderTotal.textContent,'اختر المدينة');});
