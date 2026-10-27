const {test}=require('node:test');
const assert=require('node:assert/strict');
const {initReviewRating}=require('../Orkideya/wwwroot/js/review-rating.js');
function element(dataset={}) {
 const events={},attributes={},classes=new Set();
 return {dataset,events,attributes,style:{},textContent:'',value:'',classList:{toggle(name,on){on?classes.add(name):classes.delete(name);}},setAttribute:(name,value)=>attributes[name]=value,addEventListener:(name,callback)=>events[name]=callback};
}
function fixture(value='3') {
 const stars=Array.from({length:5},(_,i)=>element({rating:String(i+1)}));
 const input=element();input.value=value;const selector=element(),number=element();
 initReviewRating({querySelectorAll:()=>stars,getElementById:id=>({ratingValue:input,starsSelector:selector,ratingNumber:number})[id]});
 return {stars,input,selector,number};
}
test('restored rating of 3 remains 3',()=>{const f=fixture();assert.equal(f.input.value,'3');assert.equal(f.number.textContent,'3');assert.equal(f.stars[2].attributes['aria-pressed'],'true');});
test('invalid submitted rating is not silently changed to 5',()=>{const f=fixture('0');assert.equal(f.input.value,'0');assert.equal(f.number.textContent,'0');});
test('click submits chosen rating',()=>{const f=fixture();f.stars[1].events.click({preventDefault(){}});assert.equal(f.input.value,'2');assert.equal(f.number.textContent,'2');});
for (const key of ['Enter',' ']) test(`keyboard ${JSON.stringify(key)} selects rating`,()=>{const f=fixture();let prevented=false;f.stars[3].events.keydown({key,preventDefault(){prevented=true;}});assert.equal(f.input.value,'4');assert.equal(prevented,true);});
test('unrelated keys do not change rating',()=>{const f=fixture();f.stars[3].events.keydown({key:'Tab'});assert.equal(f.input.value,'3');});
test('hover does not change submitted rating',()=>{const f=fixture();f.stars[4].events.mouseenter();assert.equal(f.input.value,'3');assert.equal(f.stars[4].style.color,'#ffc107');});
test('leaving hover restores selected state',()=>{const f=fixture();f.stars[4].events.mouseenter();f.selector.events.mouseleave();assert.equal(f.input.value,'3');assert.equal(f.stars[4].style.color,'#d0d0d0');});
test('missing markup is harmless',()=>{assert.doesNotThrow(()=>initReviewRating({querySelectorAll:()=>[],getElementById:()=>null}));});
