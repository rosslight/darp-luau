# Limitations and roadmap notes

Darp.Luau already covers a useful core, but some areas are still intentionally limited or marked as planned work.

## Current boundaries

- Thread support is not currently documented as a supported feature.
- Module support is still listed as TODO in the repository README.
- Async support through threads or coroutines is not presented as finished.
- Some higher-level generation scenarios are still evolving.

## Documentation gaps being addressed

The first version of this documentation focuses on the stable usage model:

- creating states,
- executing scripts,
- calling functions,
- using tables,
- exposing userdata,
- understanding ownership.

Future documentation can expand into:

- end-to-end host architecture guidance,
- API reference pages,
- more complete generator coverage,
- troubleshooting and migration notes.

## Recommendation for adopters

If you are evaluating the library for production use, validate the exact feature set you need against the current tests and implementation rather than assuming every planned capability already exists.
