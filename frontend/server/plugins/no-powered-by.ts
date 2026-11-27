// Nuxt's renderer hard-codes `x-powered-by: Nuxt`; drop it so the stack is not advertised.
export default defineNitroPlugin(nitroApp => {
  nitroApp.hooks.hook('render:response', response => {
    if (response.headers) delete (response.headers as Record<string, string>)['x-powered-by']
  })
  nitroApp.hooks.hook('beforeResponse', event => {
    event.res.headers.delete('x-powered-by')
  })
})
